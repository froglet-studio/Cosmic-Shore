using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Engine;
using CosmicShore.Engine.Rendering;
using CosmicShore.Game;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Xunit;
using Graphics = CosmicShore.Engine.Graphics;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// The engine Mesh arc: original-contract Mesh/MeshFilter/MeshCollider/
// SkinnedMeshRenderer.BakeMesh, real primitive meshes in CreatePrimitive,
// Matrix4x4 TRS, the Graphics.RenderMeshInstanced submission recorder,
// AnimationCurve — plus the ported mesh generators (Octahedron / Stellated /
// Icosphere), the prism shields (Box ↔ convex MeshCollider swap, mass scales
// with volume), the restored SegmentSpawner super-shield diagnostic, and the
// restored CapsuleMembrane instanced-draw internals.
// ─────────────────────────────────────────────────────────────────────────────

public class MeshDataTests
{
    [Fact]
    public void Mesh_BufferGetters_ReturnCopies_SettersCopyIn()
    {
        var mesh = new Mesh { name = "M" };
        var verts = new[] { Vector3.zero, Vector3.one, Vector3.up };
        mesh.vertices = verts;

        verts[0] = new Vector3(9f, 9f, 9f);                    // caller's array mutation must not leak in
        Assert.Equal(Vector3.zero, mesh.vertices[0]);

        var outVerts = mesh.vertices;
        outVerts[1] = new Vector3(7f, 7f, 7f);                 // returned copy mutation must not leak back
        Assert.Equal(Vector3.one, mesh.vertices[1]);
        Assert.Equal(3, mesh.vertexCount);
        Assert.Equal("M", mesh.name);
        Assert.Equal(IndexFormat.UInt16, mesh.indexFormat);    // original default
        mesh.MarkDynamic();                                    // no-op, must not throw
    }

    [Fact]
    public void Mesh_Triangles_SetResetsToOneSubmesh_GetConcatenates()
    {
        var mesh = new Mesh();
        mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
        mesh.SetTriangles(new List<int> { 3, 4, 5 }, 1);       // auto-grows
        Assert.Equal(2, mesh.subMeshCount);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, mesh.triangles);
        Assert.Equal(new[] { 3, 4, 5 }, mesh.GetTriangles(1));

        mesh.triangles = new[] { 6, 7, 8 };                    // collapses to submesh 0
        Assert.Equal(1, mesh.subMeshCount);
        Assert.Equal(new[] { 6, 7, 8 }, mesh.triangles);
    }

    [Fact]
    public void Mesh_RecalculateBounds_MinMaxOverVertices_AndSettable()
    {
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(-1f, 0f, 2f), new Vector3(3f, -4f, 2f), new Vector3(1f, 2f, 6f) },
        };
        mesh.RecalculateBounds();
        Assert.Equal(new Vector3(1f, -1f, 4f), mesh.bounds.center);
        Assert.Equal(new Vector3(4f, 6f, 4f), mesh.bounds.size);

        mesh.bounds = new Bounds(Vector3.one, Vector3.one * 2f); // settable (original contract)
        Assert.Equal(Vector3.one, mesh.bounds.center);

        var empty = new Mesh();
        empty.RecalculateBounds();
        Assert.Equal(Vector3.zero, empty.bounds.size);
    }

    [Fact]
    public void Mesh_RecalculateNormals_SmoothsSharedVertices()
    {
        // Flat quad in the XZ plane (two triangles sharing an edge) → all normals +Y.
        var mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 1f), new Vector3(0f, 0f, 1f),
            },
            triangles = new[] { 0, 3, 2, 0, 2, 1 },
        };
        mesh.RecalculateNormals();
        foreach (var n in mesh.normals)
        {
            Assert.Equal(1f, n.y, 4);
            Assert.Equal(0f, n.x, 4);
            Assert.Equal(0f, n.z, 4);
        }
    }

    [Fact]
    public void Mesh_Clear_DropsEverything()
    {
        var mesh = new Mesh
        {
            vertices = new[] { Vector3.one },
            normals = new[] { Vector3.up },
            uv = new[] { new Vector2(1f, 1f) },
            colors = new[] { Color.red },
            triangles = new[] { 0, 0, 0 },
        };
        mesh.subMeshCount = 3;
        mesh.RecalculateBounds();

        mesh.Clear();
        Assert.Equal(0, mesh.vertexCount);
        Assert.Empty(mesh.triangles);
        Assert.Empty(mesh.normals);
        Assert.Empty(mesh.uv);
        Assert.Empty(mesh.colors);
        Assert.Equal(1, mesh.subMeshCount);
        Assert.Equal(Vector3.zero, mesh.bounds.size);
    }
}

public class MeshComponentTests : IDisposable
{
    readonly GameLoop loop = new();
    public void Dispose() => loop.Dispose();

    [Fact]
    public void MeshFilter_SharedMesh_IsPlainReference_MeshInstancesOnAccess()
    {
        var shared = new Mesh { name = "Hull", vertices = new[] { Vector3.one } };
        var go = new GameObject("m");
        var filter = go.AddComponent<MeshFilter>();

        filter.sharedMesh = shared;
        Assert.Same(shared, filter.sharedMesh);                // no clone on sharedMesh

        var instance = filter.mesh;                            // instance-on-access
        Assert.NotSame(shared, instance);
        Assert.Equal("Hull Instance", instance.name);
        Assert.Equal(1, instance.vertexCount);
        Assert.Same(instance, filter.mesh);                    // cached
        Assert.Same(instance, filter.sharedMesh);              // original contract: sharedMesh now points at the instance

        instance.vertices = new[] { Vector3.zero, Vector3.up };
        Assert.Equal(1, shared.vertexCount);                   // the original asset is untouched
    }

    [Fact]
    public void SkinnedMeshRenderer_BakeMesh_CopiesSharedMesh()
    {
        var source = new Mesh { vertices = new[] { Vector3.up, Vector3.down }, triangles = new[] { 0, 1, 0 } };
        var go = new GameObject("smr");
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = source;

        var baked = new Mesh();
        smr.BakeMesh(baked);
        Assert.Equal(2, baked.vertexCount);
        Assert.Equal(new[] { 0, 1, 0 }, baked.triangles);

        smr.sharedMesh = null;
        smr.BakeMesh(baked);                                   // no shared mesh → cleared snapshot
        Assert.Equal(0, baked.vertexCount);
    }

    [Fact]
    public void Renderer_Bounds_UsesMeshExtentsWhenAvailable_UnitCubeOtherwise()
    {
        var go = new GameObject("r");
        go.transform.position = new Vector3(10f, 0f, 0f);
        var renderer = go.AddComponent<MeshRenderer>();
        Assert.Equal(Vector3.one, renderer.bounds.size);       // no mesh → unit-cube convention

        var mesh = new Mesh { vertices = new[] { new Vector3(-2f, -1f, -3f), new Vector3(2f, 1f, 3f) } };
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        Assert.Equal(new Vector3(4f, 2f, 6f), renderer.bounds.size);
        Assert.Equal(new Vector3(10f, 0f, 0f), renderer.bounds.center);
    }

    [Fact]
    public void CreatePrimitive_FillsRealMeshes_AndSharesThemAcrossInstances()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
        Assert.Equal(24, cubeMesh.vertexCount);                // 4 verts × 6 flat faces
        Assert.Equal(36, cubeMesh.triangles.Length);
        Assert.Equal(Vector3.one, cubeMesh.bounds.size);
        Assert.Equal(Vector3.one, cube.GetComponent<BoxCollider>().size);

        var cube2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Assert.Same(cubeMesh, cube2.GetComponent<MeshFilter>().sharedMesh); // built-ins are shared assets

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
        Assert.True(sphereMesh.vertexCount > 0);
        foreach (var v in sphereMesh.vertices)
            Assert.Equal(0.5f, v.magnitude, 3);                // radius 0.5, every vertex on the sphere
        Assert.Equal(0.5f, sphere.GetComponent<SphereCollider>().radius);

        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        var capsuleMesh = capsule.GetComponent<MeshFilter>().sharedMesh;
        Assert.Equal(2f, capsuleMesh.bounds.size.y, 3);        // radius 0.5, total height 2
        Assert.Equal(1f, capsuleMesh.bounds.size.x, 3);
        Assert.Equal(new Vector3(1f, 2f, 1f), capsule.GetComponent<BoxCollider>().size);
    }
}

public class MeshColliderTriggerTests : IDisposable
{
    readonly GameLoop loop = new();
    public void Dispose() => loop.Dispose();

    static Mesh UnitCubeMesh()
    {
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f) },
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    class TriggerRecorder : MonoBehaviour
    {
        public readonly List<Collider> Enters = new();
        public readonly List<Collider> Exits = new();
        void OnTriggerEnter(Collider other) => Enters.Add(other);
        void OnTriggerExit(Collider other) => Exits.Add(other);
    }

    [Fact]
    public void MeshCollider_DispatchesTriggerEnterAndExit_AsMeshBoundsAabb()
    {
        var meshGo = new GameObject("mesh");
        meshGo.transform.position = new Vector3(5f, 0f, 0f);
        meshGo.transform.localScale = new Vector3(2f, 1f, 1f);  // AABB extents (1, 0.5, 0.5)
        var meshCollider = meshGo.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = UnitCubeMesh();
        var recorder = meshGo.AddComponent<TriggerRecorder>();

        var probe = new GameObject("probe");
        probe.transform.position = new Vector3(2f, 0f, 0f);      // gap 3 > 1 + 1 → no contact
        var trigger = probe.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1f;

        loop.Tick(0.02f);
        Assert.Empty(recorder.Enters);

        probe.transform.position = new Vector3(3.5f, 0f, 0f);    // gap 1.5 < 2 → overlap
        loop.Tick(0.02f);
        Assert.Single(recorder.Enters);
        Assert.Same(trigger, recorder.Enters[0]);

        probe.transform.position = new Vector3(-10f, 0f, 0f);
        loop.Tick(0.02f);
        Assert.Single(recorder.Exits);
    }

    [Fact]
    public void MeshCollider_WithoutMesh_NeverOverlaps()
    {
        var meshGo = new GameObject("empty-mesh");
        var meshCollider = meshGo.AddComponent<MeshCollider>();  // sharedMesh stays null
        var recorder = meshGo.AddComponent<TriggerRecorder>();

        var probe = new GameObject("probe");
        var trigger = probe.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 5f;                                     // would swallow it if it had bounds

        loop.Tick(0.02f);
        Assert.Empty(recorder.Enters);
    }

    [Fact]
    public void MeshCollider_VisibleToSpatialQueries()
    {
        var meshGo = new GameObject("mesh");
        meshGo.transform.position = new Vector3(3f, 0f, 0f);
        var meshCollider = meshGo.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = UnitCubeMesh();

        var results = new Collider[4];
        Assert.Equal(1, Physics.OverlapSphereNonAlloc(Vector3.zero, 4f, results));
        Assert.Same(meshCollider, results[0]);
        Assert.Equal(0, Physics.OverlapSphereNonAlloc(Vector3.zero, 2f, results));

        Assert.True(Physics.CheckBox(new Vector3(2f, 0f, 0f), Vector3.one));      // touches x∈[2.5,3.5]
        Assert.False(Physics.CheckBox(new Vector3(-2f, 0f, 0f), Vector3.one));
    }
}

public class MatrixAndCurveTests
{
    [Fact]
    public void Matrix4x4_Trs_MatchesManualComposition()
    {
        var pos = new Vector3(3f, -2f, 5f);
        var rot = Quaternion.Euler(30f, 60f, -45f);
        var scale = new Vector3(2f, 0.5f, 3f);
        var m = Matrix4x4.TRS(pos, rot, scale);

        var point = new Vector3(1f, 2f, -1.5f);
        Vector3 expected = rot * Vector3.Scale(point, scale) + pos;
        Vector3 actual = m.MultiplyPoint3x4(point);
        Assert.Equal(expected.x, actual.x, 3);
        Assert.Equal(expected.y, actual.y, 3);
        Assert.Equal(expected.z, actual.z, 3);

        Vector3 dir = m.MultiplyVector(Vector3.forward);          // translation ignored
        Vector3 expectedDir = rot * Vector3.Scale(Vector3.forward, scale);
        Assert.Equal(expectedDir.z, dir.z, 3);

        Assert.Equal(Matrix4x4.identity, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one));
        Assert.Equal(new Vector4(pos.x, pos.y, pos.z, 1f), m.GetColumn(3));
    }

    [Fact]
    public void Matrix4x4_Product_ComposesTransforms()
    {
        var translate = Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one);
        var scale = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 2f);
        Vector3 result = (translate * scale).MultiplyPoint3x4(Vector3.one); // scale first, then translate
        Assert.Equal(new Vector3(3f, 2f, 2f), result);
    }

    [Fact]
    public void AnimationCurve_EaseInOut_IsSmoothstepShaped_AndClamps()
    {
        var curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        Assert.Equal(0f, curve.Evaluate(0f), 5);
        Assert.Equal(1f, curve.Evaluate(1f), 5);
        Assert.Equal(0.5f, curve.Evaluate(0.5f), 5);
        Assert.Equal(0.15625f, curve.Evaluate(0.25f), 5);         // 3t² − 2t³ (zero-tangent Hermite)
        Assert.Equal(0f, curve.Evaluate(-2f), 5);                 // clamp before first key
        Assert.Equal(1f, curve.Evaluate(2f), 5);                  // clamp after last key
    }

    [Fact]
    public void AnimationCurve_Linear_AndKeyManagement()
    {
        var curve = AnimationCurve.Linear(0f, 0f, 2f, 4f);
        Assert.Equal(2f, curve.Evaluate(1f), 5);
        Assert.Equal(2, curve.length);

        curve.AddKey(1f, 0f);                                     // keys stay time-sorted
        Assert.Equal(1f, curve[1].time);
        Assert.Equal(0f, curve.Evaluate(1f), 5);
    }
}

public class GraphicsRecorderTests
{
    [Fact]
    public void RenderMeshInstanced_RecordsBoundedSubmissions()
    {
        Graphics.ClearRecordedSubmissions();
        var mesh = new Mesh { vertices = new[] { Vector3.zero } };
        var rp = new RenderParams((Material)null)
        {
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            renderingLayerMask = 4,
        };
        var matrices = new Matrix4x4[5];

        Graphics.RenderMeshInstanced(rp, mesh, 0, matrices);
        Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, instanceCount: 3);
        Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, instanceCount: 99); // clamps to available
        Graphics.RenderMeshInstanced(rp, null, 0, matrices);                    // invalid → ignored

        Assert.Equal(3, Graphics.InstancedSubmissionCount);
        var subs = Graphics.InstancedSubmissions;
        Assert.Equal(5, subs[0].instanceCount);
        Assert.Equal(3, subs[1].instanceCount);
        Assert.Equal(5, subs[2].instanceCount);
        Assert.Same(mesh, subs[2].mesh);
        Assert.Equal(ShadowCastingMode.Off, subs[2].renderParams.shadowCastingMode);
        Assert.Equal(4u, subs[2].renderParams.renderingLayerMask);

        for (int i = 0; i < Graphics.MaxRecordedSubmissions + 7; i++)
            Graphics.RenderMeshInstanced(rp, mesh, 0, matrices);
        Assert.Equal(Graphics.MaxRecordedSubmissions, Graphics.InstancedSubmissions.Length); // ring-bounded
        Assert.Equal(3 + Graphics.MaxRecordedSubmissions + 7, Graphics.InstancedSubmissionCount);

        Graphics.ClearRecordedSubmissions();
    }
}

public class MeshGeneratorTests
{
    [Fact]
    public void Octahedron_Generates24FlatShadedVerts_8Faces()
    {
        var half = new Vector3(1f, 2f, 3f);
        var mesh = OctahedronMeshGenerator.Generate(half);
        Assert.Equal(24, mesh.vertexCount);
        Assert.Equal(24, mesh.triangles.Length);
        Assert.Equal(24, mesh.normals.Length);
        // Circumscribing dual: semi-axes 3·halfExtents → bounds size 6·halfExtents.
        Assert.Equal(new Vector3(6f, 12f, 18f), mesh.bounds.size);
    }

    [Fact]
    public void Octahedron_ContainsPointLocal_BoundaryCases()
    {
        var half = new Vector3(1f, 2f, 3f);
        Assert.True(OctahedronMeshGenerator.ContainsPointLocal(Vector3.zero, half));           // center
        Assert.True(OctahedronMeshGenerator.ContainsPointLocal(new Vector3(3f, 0f, 0f), half)); // vertex, sum = 1
        Assert.True(OctahedronMeshGenerator.ContainsPointLocal(half, half));                   // box corner, sum = 1 exactly
        Assert.False(OctahedronMeshGenerator.ContainsPointLocal(new Vector3(3.001f, 0f, 0f), half));
        Assert.True(OctahedronMeshGenerator.ContainsPointLocal(new Vector3(2f, 2f, 0f), half));   // 2/3 + 2/6 = 1 exactly (face)
        Assert.False(OctahedronMeshGenerator.ContainsPointLocal(new Vector3(2.5f, 2f, 0f), half)); // 2.5/3 + 2/6 > 1
        Assert.Equal(4.5f, OctahedronMeshGenerator.SHIELD_TO_BOX_VOLUME_RATIO);                // frozen mass ratio
        Assert.Equal(3f, OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE);
    }

    [Fact]
    public void Octahedron_FaceScale_KeepsTopologyStable_AndCollapsesToCentroids()
    {
        var half = new Vector3(1f, 1f, 1f);
        var reference = new Mesh();
        OctahedronMeshGenerator.PopulateMesh(reference, half);

        var morph = new Mesh();
        OctahedronMeshGenerator.PopulateMeshFaceScale(morph, half, faceScale: 1f);
        Assert.Equal(reference.triangles, morph.triangles);       // topology identical at any scale
        var fullVerts = reference.vertices;
        var morphVerts = morph.vertices;
        for (int i = 0; i < reference.vertexCount; i++)
            Assert.True((fullVerts[i] - morphVerts[i]).magnitude < 1e-5f); // faceScale 1 == full octahedron

        OctahedronMeshGenerator.PopulateMeshFaceScale(morph, half, faceScale: 0f);
        Assert.Equal(reference.triangles, morph.triangles);
        var refVerts = reference.vertices;
        var collapsed = morph.vertices;
        for (int f = 0; f < 8; f++)
        {
            Vector3 centroid = (refVerts[f * 3] + refVerts[f * 3 + 1] + refVerts[f * 3 + 2]) * (1f / 3f);
            for (int k = 0; k < 3; k++)
                Assert.True((collapsed[f * 3 + k] - centroid).magnitude < 1e-4f);
        }
    }

    [Fact]
    public void Octahedron_FaceShatter_DisplacesAlongFaceNormals()
    {
        var half = Vector3.one;
        var reference = new Mesh();
        OctahedronMeshGenerator.PopulateMesh(reference, half);
        var refVerts = reference.vertices;
        var refNorms = reference.normals;

        var shatter = new Mesh();
        OctahedronMeshGenerator.PopulateMeshFaceShatter(shatter, half, faceScale: 0f, faceOffset: 2f);
        var verts = shatter.vertices;
        for (int f = 0; f < 8; f++)
        {
            Vector3 centroid = (refVerts[f * 3] + refVerts[f * 3 + 1] + refVerts[f * 3 + 2]) * (1f / 3f);
            Vector3 expected = centroid + 2f * refNorms[f * 3];
            for (int k = 0; k < 3; k++)
                Assert.True((verts[f * 3 + k] - expected).magnitude < 1e-4f);
        }
    }

    [Fact]
    public void StellatedOctahedron_Generates72Verts_24Faces_AndContainment()
    {
        var half = new Vector3(1f, 2f, 3f);
        var mesh = StellatedOctahedronMeshGenerator.Generate(half);
        Assert.Equal(StellatedOctahedronMeshGenerator.VERTEX_COUNT, mesh.vertexCount); // 72
        Assert.Equal(72, mesh.triangles.Length);                                       // 24 faces

        Assert.True(StellatedOctahedronMeshGenerator.ContainsPointLocal(Vector3.zero, half));
        Assert.True(StellatedOctahedronMeshGenerator.ContainsPointLocal(new Vector3(3f, 0f, 0f), half)); // octahedron vertex
        Assert.True(StellatedOctahedronMeshGenerator.ContainsPointLocal(new Vector3(3f, 6f, 9f), half)); // spike tip (cube corner)
        Assert.False(StellatedOctahedronMeshGenerator.ContainsPointLocal(new Vector3(3.1f, 6.2f, 9.3f), half));
        // Between two spikes but outside both tetrahedra (near a stellation "valley"):
        Assert.False(StellatedOctahedronMeshGenerator.ContainsPointLocal(new Vector3(2.9f, 5.8f, 0f), half));

        Assert.Equal(13.5f, StellatedOctahedronMeshGenerator.SUPER_SHIELD_TO_BOX_VOLUME_RATIO);
        Assert.Equal(3f, StellatedOctahedronMeshGenerator.SUPER_SHIELD_TO_OCTAHEDRON_VOLUME_RATIO);
    }

    [Fact]
    public void StellatedOctahedron_FaceScale_TopologyStable()
    {
        var half = Vector3.one;
        var reference = new Mesh();
        StellatedOctahedronMeshGenerator.PopulateMesh(reference, half);
        var morph = new Mesh();
        StellatedOctahedronMeshGenerator.PopulateMeshFaceScale(morph, half, faceScale: 0.5f);
        Assert.Equal(reference.triangles, morph.triangles);
        Assert.Equal(reference.vertexCount, morph.vertexCount);
    }

    [Fact]
    public void Icosphere_SubdivisionQuadruplesFaces_AllVertsOnRadius()
    {
        var level0 = IcosphereMeshGenerator.Generate(subdivisions: 0, radius: 2f);
        Assert.Equal(60, level0.vertexCount);                     // flat-shaded: 20 tris × 3
        Assert.Equal(60, level0.triangles.Length);

        var level2 = IcosphereMeshGenerator.Generate(subdivisions: 2, radius: 2f);
        Assert.Equal(960, level2.vertexCount);                    // 320 tris × 3
        foreach (var v in level2.vertices)
            Assert.Equal(2f, v.magnitude, 3);

        var smooth = IcosphereMeshGenerator.Generate(subdivisions: 1, radius: 1f, flatShaded: false);
        Assert.Equal(42, smooth.vertexCount);                     // 12 + 30 shared midpoints
        Assert.Equal(240, smooth.triangles.Length);               // 80 tris
        Assert.Equal(CosmicShore.Engine.Rendering.IndexFormat.UInt32, smooth.indexFormat);
    }
}

public class PrismShieldTests : IDisposable
{
    readonly GameLoop loop = new();
    public void Dispose() => loop.Dispose();

    static (GameObject go, BoxCollider box, MeshFilter filter, Rigidbody rb, Mesh originalMesh)
        MakeShieldHost(Vector3 size)
    {
        var go = new GameObject("prism-host");
        var box = go.AddComponent<BoxCollider>();
        box.size = size;
        var filter = go.AddComponent<MeshFilter>();
        var original = new Mesh { name = "AuthoredBox", vertices = new[] { Vector3.zero } };
        filter.sharedMesh = original;
        go.AddComponent<MeshRenderer>();
        var rb = go.AddComponent<Rigidbody>();
        return (go, box, filter, rb, original);
    }

    [Fact]
    public void OctahedronShield_EngageInstant_SwapsMeshCollidersAndMass()
    {
        var (go, box, filter, rb, original) = MakeShieldHost(new Vector3(2f, 4f, 6f)); // half-extents (1,2,3)
        var shield = go.AddComponent<PrismOctahedronShield>();

        shield.Engage(instant: true);

        Assert.True(shield.IsShielded);
        Assert.False(box.enabled);
        var meshCollider = go.GetComponent<MeshCollider>();
        Assert.True(meshCollider.enabled);
        Assert.True(meshCollider.convex);
        Assert.Equal(24, meshCollider.sharedMesh.vertexCount);    // octahedron
        Assert.Same(meshCollider.sharedMesh, filter.sharedMesh);  // visual = collision mesh
        Assert.Equal(36f * 1f * 2f * 3f, rb.mass, 3);             // ρ·36·a·b·c, density 1

        shield.Disengage(instant: true);
        Assert.False(shield.IsShielded);
        Assert.True(box.enabled);
        Assert.False(meshCollider.enabled);
        Assert.Same(original, filter.sharedMesh);                 // authored mesh restored
        Assert.Equal(8f * 1f * 2f * 3f, rb.mass, 3);              // ρ·8·a·b·c
    }

    [Fact]
    public void OctahedronShield_AnimatedEngage_BloomsThenAppliesShieldedPose()
    {
        var (go, box, filter, _, _) = MakeShieldHost(Vector3.one);
        var shield = go.AddComponent<PrismOctahedronShield>();

        shield.Engage();                                          // engageDuration 0.35s
        Assert.True(shield.IsShielded);
        Assert.True(shield.IsTransitioning);
        Assert.False(box.enabled);                                // colliders off during morph
        Assert.False(go.GetComponent<MeshCollider>());            // not created until pose applies

        for (int i = 0; i < 5; i++) loop.Tick(0.1f);              // 0.5s > engageDuration

        Assert.False(shield.IsTransitioning);
        Assert.Equal(1f, shield.TransitionProgress, 3);
        var meshCollider = go.GetComponent<MeshCollider>();
        Assert.True(meshCollider.enabled);
        Assert.Equal(24, filter.sharedMesh.vertexCount);
    }

    [Fact]
    public void OctahedronShield_Disengage_PlaysShatterOverlayChild()
    {
        var (go, _, filter, _, original) = MakeShieldHost(Vector3.one);
        var shield = go.AddComponent<PrismOctahedronShield>();
        shield.Engage(instant: true);

        shield.Disengage();                                       // shatterDuration 0.6s
        Assert.Same(original, filter.sharedMesh);                 // box snaps back immediately
        Assert.True(shield.IsTransitioning);
        var overlay = go.transform.Cast<Transform>().FirstOrDefault(t => t.gameObject.name == "ShieldShatter");
        Assert.NotNull(overlay);
        Assert.True(overlay.gameObject.activeSelf);

        for (int i = 0; i < 8; i++) loop.Tick(0.1f);              // ride out the shatter
        Assert.False(shield.IsTransitioning);
        Assert.False(overlay.gameObject.activeSelf);
    }

    [Fact]
    public void OctahedronShield_IsPointInsideShield_TracksWorldPose()
    {
        var (go, _, _, _, _) = MakeShieldHost(Vector3.one);       // half-extents 0.5 → semi-axes 1.5
        go.transform.position = new Vector3(10f, 0f, 0f);
        var shield = go.AddComponent<PrismOctahedronShield>();
        shield.Engage(instant: true);

        Assert.True(shield.IsPointInsideShield(new Vector3(10f, 0f, 0f)));
        Assert.True(shield.IsPointInsideShield(new Vector3(11.5f, 0f, 0f)));   // on the vertex
        Assert.False(shield.IsPointInsideShield(new Vector3(11.6f, 0f, 0f)));
        Assert.False(shield.IsPointInsideShield(new Vector3(11f, 1f, 0f)));    // 1 + 2/3 > 1
    }

    [Fact]
    public void StellatedShield_EngageInstant_UsesStellationMassAndMesh()
    {
        var (go, box, filter, rb, _) = MakeShieldHost(new Vector3(2f, 2f, 2f)); // half-extents (1,1,1)
        var shield = go.AddComponent<PrismStellatedOctahedronShield>();

        shield.Engage(instant: true);

        Assert.True(shield.IsShielded);
        Assert.False(box.enabled);
        var meshCollider = go.GetComponent<MeshCollider>();
        Assert.True(meshCollider.convex);
        Assert.Equal(StellatedOctahedronMeshGenerator.VERTEX_COUNT, filter.sharedMesh.vertexCount); // 72
        Assert.Equal(108f, rb.mass, 3);                            // ρ·108·a·b·c

        // Spike tip (cube corner at 3·halfExtents) is inside; beyond it is not.
        Assert.True(shield.IsPointInsideShield(new Vector3(3f, 3f, 3f)));
        Assert.False(shield.IsPointInsideShield(new Vector3(3.2f, 3.2f, 3.2f)));

        shield.Disengage(instant: true);
        Assert.Equal(8f, rb.mass, 3);
    }

    [Fact]
    public void Shield_OnDisable_SnapsBackToCleanUnshieldedState()
    {
        var (go, box, filter, _, original) = MakeShieldHost(Vector3.one);
        var shield = go.AddComponent<PrismOctahedronShield>();
        shield.Engage(instant: true);

        go.SetActive(false);                                       // pool-return path

        Assert.False(shield.IsShielded);
        Assert.False(shield.IsTransitioning);
        Assert.Same(original, filter.sharedMesh);
        Assert.True(box.enabled);
    }
}

public class SegmentSpawnerSuperShieldTests : IDisposable
{
    readonly GameLoop loop = new();
    public void Dispose() => loop.Dispose();

    static readonly BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void SuperShieldSpawnedPrisms_AttachesEngagedStellatedShields_AndSetsCanonicalFlag()
    {
        // The restored diagnostic block: every prism under the spawned container gets a
        // PrismStellatedOctahedronShield engaged + prismProperties.IsSuperShielded.
        var spawnerGo = new GameObject("segment-spawner");
        spawnerGo.SetActive(false);
        var spawner = spawnerGo.AddComponent<SegmentSpawner>();

        var container = new GameObject("SpawnedSegments");
        var rig = PrismTestRig.Create("track-prism");
        rig.Prism.prismProperties.IsShielded = true;               // ShieldedSpawnablePrism authoring
        rig.GameObject.transform.SetParent(container.transform, false);

        typeof(SegmentSpawner).GetField("SpawnedSegmentContainer", Priv)!.SetValue(spawner, container);
        typeof(SegmentSpawner).GetMethod("SuperShieldSpawnedPrisms", Priv)!.Invoke(spawner, null);

        var shield = rig.GameObject.GetComponent<PrismStellatedOctahedronShield>();
        Assert.True(shield);                                       // attached by the restored block
        Assert.True(shield.IsShielded);                            // engaged (instant default)
        Assert.False(shield.IsTransitioning);
        Assert.False(rig.Prism.prismProperties.IsShielded);        // legacy flag cleared first
        Assert.True(rig.Prism.prismProperties.IsSuperShielded);    // canonical invulnerability flag

        var meshCollider = rig.GameObject.GetComponent<MeshCollider>();
        Assert.True(meshCollider.enabled && meshCollider.convex);
        Assert.Equal(StellatedOctahedronMeshGenerator.VERTEX_COUNT, meshCollider.sharedMesh.vertexCount);
    }
}

public class CapsuleMembraneDrawTests : IDisposable
{
    readonly GameLoop loop = new();
    public void Dispose() => loop.Dispose();

    static readonly BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void Membrane_FallbackPath_SubmitsOneInstancedDrawPerFrame()
    {
        Graphics.ClearRecordedSubmissions();

        var go = new GameObject("membrane");
        go.SetActive(false);
        var membrane = go.AddComponent<CapsuleMembrane>();
        typeof(CapsuleMembrane).GetField("subdivisions", Priv)!.SetValue(membrane, 1);   // 42 capsules
        typeof(CapsuleMembrane).GetField("membraneMaterial", Priv)!.SetValue(membrane, new Material((Shader)null));
        go.SetActive(true);                                        // Awake: builds matrices + renderParams

        loop.Tick(0.02f);
        loop.Tick(0.02f);

        Assert.Equal(2, Graphics.InstancedSubmissionCount);        // one RenderMeshInstanced per Update
        var last = Graphics.InstancedSubmissions[^1];
        Assert.Equal(42, last.instanceCount);                      // icosphere level 1 vertex count
        Assert.Equal(ShadowCastingMode.Off, last.renderParams.shadowCastingMode);
        Assert.False(last.renderParams.receiveShadows);

        // The mesh is the built-in capsule primitive (GetBuiltinCapsuleMesh restored).
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Assert.Same(capsule.GetComponent<MeshFilter>().sharedMesh, last.mesh);

        // Matrices are real TRS transforms on the membrane sphere (radius 500 ± jitter).
        for (int i = 0; i < last.instanceCount; i++)
        {
            var column = last.instanceData[i].GetColumn(3);
            float dist = new Vector3(column.x, column.y, column.z).magnitude;
            Assert.InRange(dist, 500f * 0.9f, 500f * 1.1f);
        }

        Graphics.ClearRecordedSubmissions();
    }
}
