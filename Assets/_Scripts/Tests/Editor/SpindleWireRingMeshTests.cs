using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The runtime-built switch-ring wire (<see cref="SpindleWireRingMesh"/>) against the meshes
    /// author_spindle_lod_meshes.py baked (Docs/GENOME_MESH_AT_LOAD.md). Outside Unity the same
    /// comparison runs on the raw bytes: Tools/Build/genome_mesh_parity/run.sh.
    /// </summary>
    public class SpindleWireRingMeshTests
    {
        const string Folder = "Assets/_Models/Spindles/";
        const float Tol = 1e-4f;

        [TestCase(SpindleWireRingMesh.Tier.Near, "SpindleWireRing")]
        [TestCase(SpindleWireRingMesh.Tier.Low, "SpindleWireRingLow")]
        [TestCase(SpindleWireRingMesh.Tier.Thread, "SpindleWireRingThread")]
        public void BuiltMeshMatchesShippedAsset(SpindleWireRingMesh.Tier tier, string asset)
        {
            var shipped = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + asset + ".asset");
            Assert.IsNotNull(shipped, asset + " not found");
            var built = SpindleWireRingMesh.Build(tier);
            try
            {
                Assert.AreEqual(shipped.vertexCount, built.vertexCount, "vertex count");
                CollectionAssert.AreEqual(shipped.triangles, built.triangles, "index buffer");
                AssertClose(shipped.vertices, built.vertices, "positions");
                AssertClose(shipped.normals, built.normals, "normals");
                var a = shipped.tangents; var b = built.tangents;
                for (int i = 0; i < a.Length; i++)
                    Assert.Less(((Vector4)(a[i] - b[i])).magnitude, Tol, "tangent " + i);
                var ua = shipped.uv; var ub = built.uv;
                for (int i = 0; i < ua.Length; i++)
                    Assert.Less((ua[i] - ub[i]).magnitude, Tol, "uv0 " + i);
                Assert.Less((shipped.bounds.center - built.bounds.center).magnitude, Tol, "bounds centre");
                Assert.Less((shipped.bounds.extents - built.bounds.extents).magnitude, Tol, "bounds extents");
            }
            finally { Object.DestroyImmediate(built); }
        }

        [Test]
        public void GetReturnsOneSharedMeshPerTier()
        {
            Assert.AreSame(SpindleWireRingMesh.Get(SpindleWireRingMesh.Tier.Low), SpindleWireRingMesh.Get(SpindleWireRingMesh.Tier.Low));
            Assert.AreNotSame(SpindleWireRingMesh.Get(SpindleWireRingMesh.Tier.Low), SpindleWireRingMesh.Get(SpindleWireRingMesh.Tier.Thread));
        }

        static void AssertClose(Vector3[] a, Vector3[] b, string what)
        {
            Assert.AreEqual(a.Length, b.Length, what + " length");
            for (int i = 0; i < a.Length; i++)
                Assert.Less((a[i] - b[i]).magnitude, Tol, what + " " + i);
        }
    }
}
