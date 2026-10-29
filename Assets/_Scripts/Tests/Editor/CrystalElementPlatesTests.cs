#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The omni crystal wears all four elements' shades of lime, each on its own shapes (Garrett, 2026-10-28: "change the
    /// omnicrystal to retain all four elemental colors of lime in all instances"; Docs/PALETTE.md §2.12):
    /// <see cref="CrystalElementPlates"/> names every plate's element on the shipped body, every twin the body draws keeps
    /// it, and the shipped materials carry the palette's shades.
    /// </summary>
    public class CrystalElementPlatesTests
    {
        const string OmniPrefabPath = "Assets/_Prefabs/Environment/Crystal.prefab";
        const string PalettePath = "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset";
        const string MaterialDir = "Assets/_Graphics/Materials/CrystalMaterials";

        static Mesh ShippedBody()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OmniPrefabPath);
            Assert.IsNotNull(prefab, $"{OmniPrefabPath} not found");
            var body = prefab.GetComponent<Crystal>().CrystalModels[0].model;
            Assert.IsTrue(body && body.TryGetComponent(out SkinnedMeshRenderer renderer) && renderer.sharedMesh,
                "the omni's body (crystalModels slot 0) draws no skinned mesh");
            return body.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        }

        /// <summary>How many PLATES of each element a mesh's channel names.</summary>
        static Dictionary<Element, int> PlatesPerElement(Mesh mesh, IReadOnlyList<Vector4> ids)
        {
            var plateOf = RhombusSkinBaker.PlateOfVertices(mesh, mesh.vertices, out _, out _, out _);
            return Enumerable.Range(0, plateOf.Length)
                .GroupBy(v => plateOf[v])
                .Select(plate => (Element)Mathf.RoundToInt(ids[plate.First()].x))
                .GroupBy(e => e)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        static List<Vector4> Ids(Mesh mesh)
        {
            var ids = new List<Vector4>();
            mesh.GetUVs(CrystalElementPlates.Channel, ids);
            return ids;
        }

        [Test]
        public void ShippedOmniBody_NamesEachShapeFamilysElement()
        {
            var source = ShippedBody();
            Assert.IsFalse(CrystalElementPlates.Carries(source), "the FBX mesh carries no element channel: the twins add it");
            var elements = CrystalElementPlates.Classify(source);
            var ids = elements.Select(e => new Vector4((int)e, 0f, 0f, 0f)).ToList();
            var plates = PlatesPerElement(source, ids);
            Assert.AreEqual(12, plates.GetValueOrDefault(Element.Charge), "12 pentagonal prisms are Charge");
            Assert.AreEqual(20, plates.GetValueOrDefault(Element.Mass), "20 triangular prisms are Mass");
            Assert.AreEqual(60, plates.GetValueOrDefault(Element.Space), "60 square slabs are Space");
            Assert.AreEqual(30, plates.GetValueOrDefault(Element.Time), "30 rhombic slabs are Time");
            Assert.AreEqual(0, plates.GetValueOrDefault(Element.None), "every plate of the omni is one of the four shapes");
        }

        [Test]
        public void EveryTwinTheBodyDrawsKeepsTheElements()
        {
            var source = ShippedBody();
            var want = CrystalElementPlates.Classify(source);
            var skin = RhombusSkinBaker.Bake(source, out var problem);
            Mesh cut = null;
            try
            {
                Assert.IsNotNull(skin, problem);
                Assert.IsTrue(CrystalElementPlates.Carries(skin.Mesh), "the skin twin names no elements");
                var ids = Ids(skin.Mesh);
                Assert.AreEqual(want.Length, ids.Count);
                for (int v = 0; v < want.Length; v++)
                    Assert.AreEqual((int)want[v], Mathf.RoundToInt(ids[v].x), $"vertex {v}");

                cut = RhombusSkinBaker.Cut(skin.Mesh, 6, out problem);
                Assert.IsNotNull(cut, problem);
                Assert.IsTrue(CrystalElementPlates.Carries(cut), "the cut twin (the triangle holes) lost the elements");
            }
            finally
            {
                if (cut) Object.DestroyImmediate(cut);
                if (skin != null) Object.DestroyImmediate(skin.Mesh);
            }
        }

        [Test]
        public void WithElements_IsAShared_RegisteredTwin_AndLeavesAShapelessMeshAlone()
        {
            var source = ShippedBody();
            var twin = CrystalElementPlates.WithElements(source);
            Assert.AreNotSame(source, twin, "the imported mesh is never edited");
            Assert.IsTrue(CrystalElementPlates.Carries(twin));
            Assert.AreSame(twin, CrystalElementPlates.WithElements(source), "one twin per mesh, shared");
            Assert.AreSame(twin, CrystalElementPlates.WithElements(twin), "a mesh that carries them is its own");
            Assert.AreSame(source, RhombusSkinBaker.SourceOf(twin), "the twin resolves back to the FBX mesh");

            var quad = new Mesh { name = "Quad" };
            quad.SetVertices(new[] { Vector3.zero, Vector3.right, Vector3.up, new Vector3(1f, 1f, 0f) });
            quad.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            try
            {
                Assert.AreSame(quad, CrystalElementPlates.WithElements(quad), "a plate of no element's shape is drawn as before");
                Assert.IsFalse(CrystalElementPlates.TryWrite(quad, null, out _));
                Assert.IsFalse(CrystalElementPlates.Carries(quad));
            }
            finally
            {
                Object.DestroyImmediate(quad);
            }
        }

        [Test]
        public void ShippedMaterials_PaintEachElementInThePalettesShade()
        {
            var palette = AssetDatabase.LoadAssetAtPath<SO_ColorSet>(PalettePath);
            Assert.IsNotNull(palette, PalettePath);
            var body = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/OmniCrystalBody.mat");
            var inactive = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/OmniCrystalBodyInactive.mat");
            var triangles = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/OmniDepthPrisms.mat");
            var arcs = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/OmniChargeArcs.mat");
            Assert.IsTrue(body && inactive && triangles && arcs, "an omni material is missing");

            Assert.AreEqual(1f, body.GetFloat("_ElementShades"), "the free omni paints each element's shapes in its shade");
            Assert.AreEqual(0f, inactive.GetFloat("_ElementShades"), "the embedded omni stays the blue-white neutral");

            var bright = body.GetColor("_BrightColor");
            foreach (var (element, property) in new[]
                     {
                         (Element.Charge, "_ChargeColor"), (Element.Mass, "_MassColor"),
                         (Element.Space, "_SpaceColor"), (Element.Time, "_TimeColor"),
                     })
            {
                Assert.IsTrue(palette.EnvironmentColors.TryGetElementalCrystalColor(element, out var shade), $"{element} has no shade");
                var want = bright.WithHueOf(shade);
                var have = body.GetColor(property);
                Assert.AreEqual(want.r, have.r, 2e-3f, $"{property}.r");
                Assert.AreEqual(want.g, have.g, 2e-3f, $"{property}.g");
                Assert.AreEqual(want.b, have.b, 2e-3f, $"{property}.b");
            }
            Assert.AreEqual(body.GetColor("_MassColor"), triangles.GetColor("_BrightColor"), "the triangle prisms are Mass's");
            Assert.AreEqual(body.GetColor("_ChargeColor"), arcs.GetColor("_BrightColor"), "the Charge light is Charge's");
        }
    }
}
#endif
