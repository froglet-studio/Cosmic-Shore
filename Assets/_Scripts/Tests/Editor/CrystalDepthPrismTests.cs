#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The omni and Mass crystals' "sealed by depth" prisms (<see cref="CrystalDepthPrisms"/>, Omni Shepard Lab
    /// round 7). These pin the ratchet's promise - in view a prism only ever moves down, and it is replaced only
    /// behind the crystal - the body's triangle holes, and the wiring the generators author
    /// (<c>Tools/Build/author_omni_crystal_triangles.py</c>, <c>author_mass_crystal_look.py</c>).
    /// </summary>
    public class CrystalDepthPrismTests
    {
        const string OmniPrefabPath = "Assets/_Prefabs/Environment/Crystal.prefab";
        const string ProfilePath = "Assets/_SO_Assets/Environment/CrystalDepthPrismProfile.asset";
        const string OmniPrismMeshPath = "Assets/_Models/OmniCrystalTriangles.asset";
        const string MassPrismMeshPath = "Assets/_Models/MassCrystalPrisms.asset";
        const string BaseMaterialSetPath = "Assets/_SO_Assets/MaterialSets/OriginalMaterialSet.asset";
        static readonly string[] MassPrefabPaths =
        {
            "Assets/_Prefabs/Environment/CrystalMass.prefab",
            "Assets/_Prefabs/Environment/ActiveCrystalMass.prefab",
        };

        // The ratchet's maths and the per-crystal pose live in DepthRatchetTests (no editor dependencies, so
        // Tools/Build/crystal_depth_harness runs them headlessly).

        // ------------------------------------------------------------------ the holes

        [Test]
        public void CutPlates_RemovesOnlyTheTrianglePrisms_AndKeepsEveryVertex()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            AddSlab(verts, tris, new Vector3(2f, 0f, 1f), new Vector2(0.3f, 0.3f), new Vector2(-0.3f, 0.3f), new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.3f)); // square
            int squareTris = tris.Count;
            AddSlab(verts, tris, new Vector3(-2f, 0f, 1f), new Vector2(0.3f, 0f), new Vector2(-0.2f, 0.25f), new Vector2(-0.2f, -0.25f)); // triangle
            var source = new Mesh { name = "Plates" };
            source.SetVertices(verts);
            source.SetTriangles(tris, 0);

            var cut = RhombusSkinBaker.Cut(source, 6, out var problem);
            try
            {
                Assert.IsNotNull(cut, problem);
                CollectionAssert.AreEqual(source.vertices, cut.vertices, "a cut must keep every vertex, in order");
                CollectionAssert.AreEqual(tris.Take(squareTris).ToArray(), cut.GetTriangles(0), "only the 6-corner plate's triangles may go");
                Assert.IsNull(RhombusSkinBaker.Cut(source, 5, out _), "a corner count no plate has must be refused, not cut nothing");
            }
            finally
            {
                if (cut) Object.DestroyImmediate(cut);
                Object.DestroyImmediate(source);
            }
        }

        // ------------------------------------------------------------------ the wiring

        [Test]
        public void OmniCrystal_DrawsItsTrianglesAsDepthPrisms_InSlotOne_AndCutsTheBody()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OmniPrefabPath);
            Assert.IsNotNull(prefab, $"{OmniPrefabPath} not found");
            var models = prefab.GetComponent<Crystal>().CrystalModels;
            Assert.AreEqual(2, models.Count, "the omni is its body (slot 0) and its depth prisms (slot 1)");
            var prisms = models[1].model;
            Assert.IsTrue(prisms.TryGetComponent(out CrystalDepthPrisms driver), "slot 1 carries no CrystalDepthPrisms");
            var serialized = new SerializedObject(driver);
            Assert.IsTrue(serialized.FindProperty("cutBodyHoles").boolValue, "the omni must cut its body's triangle holes");
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<CrystalDepthPrismProfileSO>(ProfilePath), serialized.FindProperty("profile").objectReferenceValue);
            AssertDepthMaterial(prisms.GetComponent<MeshRenderer>().sharedMaterial, "omni prisms");
            AssertDepthMaterial(models[1].defaultMaterial, "omni default");
            AssertDepthMaterial(models[1].inactiveMaterial, "omni inactive");

            // A team omni clones the base set's slot material: it must be the depth prisms too.
            var baseSet = AssetDatabase.LoadAssetAtPath<SO_MaterialSet>(BaseMaterialSetPath);
            Assert.AreSame(models[1].defaultMaterial, baseSet.CrystalMaterial1, "OriginalMaterialSet.CrystalMaterial1 is not the omni's slot-1 material");
        }

        [Test]
        public void MassCrystals_AreOneDepthPrismRenderer_WithNoHolesToCut()
        {
            foreach (var path in MassPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"{path} not found");
                var models = prefab.GetComponent<Crystal>().CrystalModels;
                Assert.AreEqual(1, models.Count, $"{path}: the Mass is one prism renderer");
                Assert.IsTrue(models[0].model.TryGetComponent(out CrystalDepthPrisms driver), $"{path}: no CrystalDepthPrisms");
                Assert.IsFalse(new SerializedObject(driver).FindProperty("cutBodyHoles").boolValue, $"{path}: the Mass has no body to cut");
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Mesh>(MassPrismMeshPath), models[0].model.GetComponent<MeshFilter>().sharedMesh, $"{path}: not on the baked prism mesh");
                AssertDepthMaterial(models[0].defaultMaterial, path);
                Assert.AreEqual(1, prefab.GetComponentsInChildren<Renderer>(true).Length, $"{path}: a retired shell is still in the prefab");
            }
        }

        [Test]
        public void PrismMeshes_CarryEachPlatesCentreAndIndex()
        {
            foreach (var (path, plates) in new[] { (OmniPrismMeshPath, 20), (MassPrismMeshPath, 60) })
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(mesh, $"{path} not found");
                Assert.IsTrue(mesh.isReadable, $"{path}: CrystalDepthPrisms reads plate centres off the mesh");
                var uv2 = new List<Vector4>();
                mesh.GetUVs(2, uv2);
                Assert.AreEqual(mesh.vertexCount, uv2.Count, $"{path}: no TEXCOORD2");
                var byPlate = uv2.GroupBy(p => Mathf.RoundToInt(p.w)).ToList();
                CollectionAssert.AreEquivalent(Enumerable.Range(0, plates), byPlate.Select(g => g.Key), $"{path}: plate indices are not 0..{plates - 1}");
                Assert.LessOrEqual(plates, CrystalDepthPrisms.MaxPlates);
                foreach (var g in byPlate)
                    Assert.AreEqual(1, g.Select(p => (Vector3)p).Distinct().Count(), $"{path}: plate {g.Key} has more than one centre");
            }
        }

        static void AssertDepthMaterial(Material material, string what)
        {
            Assert.IsTrue(material, $"{what}: no material");
            Assert.AreEqual("Custom/OmniDepthPrismShader", material.shader.name, $"{what}: not on OmniDepthPrismShader");
            Assert.AreEqual(0f, material.GetFloat("_DepthDriven"), $"{what}: an undriven prism must draw sealed and solid");
        }

        /// <summary>A flat prism: the polygon at <paramref name="centre"/> (in its xy) and a copy 0.1 deeper.</summary>
        static void AddSlab(List<Vector3> verts, List<int> tris, Vector3 centre, params Vector2[] polygon)
        {
            int n = polygon.Length, top = verts.Count;
            foreach (var p in polygon) verts.Add(centre + new Vector3(p.x, p.y, 0f));
            foreach (var p in polygon) verts.Add(centre + new Vector3(p.x, p.y, -0.1f));
            for (int i = 1; i < n - 1; i++)
            {
                tris.AddRange(new[] { top, top + i, top + i + 1 });
                tris.AddRange(new[] { top + n, top + n + i + 1, top + n + i });
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris.AddRange(new[] { top + i, top + n + i, top + j });
                tris.AddRange(new[] { top + j, top + n + i, top + n + j });
            }
        }
    }
}
#endif
