using System;
using CosmicShore.Content.Models;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

// A skinned model imported from the project's FBX files must deform onto its OWN hull at rest.
// The Blender exporter writes a cluster's "Transform" as link⁻¹·meshGlobal rather than the
// spec's mesh global, so reading it by the spec applied link⁻¹ twice: every vessel hull folded
// onto its bones (the Squirrel came out ~half size and rotated, the Manta invisibly inside out).
public class SkinBindposeTests
{
    const string SquirrelModelGuid = "5db69771d02b4c24ca00998adf318137";

    [Fact]
    public void SquirrelHull_SkinnedAtBind_LandsExactlyOnItsOwnMesh()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        var model = ContentYamlTests.Db.LoadModel(SquirrelModelGuid);
        var hull = model.FindNode("a_SquirrelShipMesh_nearfinal");
        var mesh = hull.Mesh.Mesh;
        var bp = mesh.bindposes;
        Assert.Equal(hull.Bones.Count, bp.Length);

        // Each bone at its bind global (TransformLink): bone · bindpose must be the mesh node itself.
        var meshToModel = ToEngine(hull.ModelMatrix);
        var skin = SkinFor(model, hull, bindGlobals: true);
        foreach (var m in skin)
            for (int c = 0; c < 16; c++)
                Assert.True(MathF.Abs(m[c] - meshToModel[c]) < 1e-3f, $"skin matrix differs from the mesh node at element {c}");
    }

    [Fact]
    public void SquirrelHull_SkinnedAtItsImportedPose_KeepsItsSize()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        var model = ContentYamlTests.Db.LoadModel(SquirrelModelGuid);
        var hull = model.FindNode("a_SquirrelShipMesh_nearfinal");
        var mesh = hull.Mesh.Mesh;
        var skin = SkinFor(model, hull, bindGlobals: false);
        var meshToModel = ToEngine(hull.ModelMatrix);
        Vector3 slo = Vector3.one * 1e9f, shi = -slo, rlo = slo, rhi = -slo;
        var verts = mesh.vertices; var weights = mesh.boneWeights;
        for (int v = 0; v < verts.Length; v++)
        {
            var w = weights[v];
            var p = w.weight0 * skin[w.boneIndex0].MultiplyPoint3x4(verts[v]) + w.weight1 * skin[w.boneIndex1].MultiplyPoint3x4(verts[v])
                  + w.weight2 * skin[w.boneIndex2].MultiplyPoint3x4(verts[v]) + w.weight3 * skin[w.boneIndex3].MultiplyPoint3x4(verts[v]);
            var r = meshToModel.MultiplyPoint3x4(verts[v]);
            slo = Vector3.Min(slo, p); shi = Vector3.Max(shi, p);
            rlo = Vector3.Min(rlo, r); rhi = Vector3.Max(rhi, r);
        }
        // The export frame poses the tail, so the hull may shift a little — but it keeps its span
        // (the double-inverted bindpose shrank it to roughly half and turned it on its side).
        var skinned = shi - slo; var rest = rhi - rlo;
        for (int a = 0; a < 3; a++)
            Assert.InRange(skinned[a] / rest[a], 0.7f, 1.3f);
    }

    static Matrix4x4[] SkinFor(ImportedModel model, ModelNode hull, bool bindGlobals)
    {
        var bp = hull.Mesh.Mesh.bindposes;
        var skin = new Matrix4x4[bp.Length];
        var clusters = new System.Collections.Generic.Dictionary<ModelNode, DMat4>();
        if (bindGlobals)
            foreach (var geom in hull.Source.Children)
                foreach (var def in geom.Children)
                {
                    if (def.SubClass != "Skin") continue;
                    foreach (var cluster in def.Children)
                    {
                        if (cluster.SubClass != "Cluster") continue;
                        foreach (var c in cluster.Children)
                        {
                            var bone = model.Nodes.Find(n => n.Source == c);
                            if (bone != null)
                                clusters[bone] = FbxModelImporter.ToUnity(model.AxisConversion * DMat4.FromArray(cluster.Node.DoubleArray("TransformLink")), model.UnitScale);
                        }
                    }
                }
        for (int i = 0; i < bp.Length; i++)
        {
            var bone = hull.Bones[i];
            var global = bindGlobals ? clusters[bone] : bone.ModelMatrix;
            skin[i] = ToEngine(global) * bp[i];
        }
        return skin;
    }

    static Matrix4x4 ToEngine(DMat4 m) => FbxModelImporter.ToEngine(m);
}
