#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A mesh harvester that reads a prefab ASSET sees what the asset shows, not what the ship
    /// shows. The Scarab used to generate its hull at Awake over a wrapped Sparrow model it hid at
    /// Awake, so on the asset the real hull was an empty MeshFilter beside a still-enabled Sparrow -
    /// which is how the codex baked the Scarab as a Sparrow, byte for byte, and how the toybox's mini
    /// Scarab was a Sparrow too. Since 2026-10-18 the Scarab draws a real model (Scarab.fbx,
    /// Tools/Build/vessel_model/specs/scarab.py) and the Sparrow is gone from its prefab. Holds: the
    /// asset carries the model and no Sparrow mesh, the toy harvester draws the model, and the
    /// procedural builder (kept until the owner accepts the model) still answers
    /// <see cref="IProceduralHullSource"/> with the parts its form generates.
    /// </summary>
    public class ProceduralHullHarvestTests
    {
        const string ScarabPath = "Assets/_Prefabs/Spacevessels/Scarab.prefab";
        const string ScarabModelPath = "Assets/_Models/Vessel Models/Scarab.fbx";
        const string SparrowModelPath = "Assets/_Models/Vessel Models/SparrowModel1.fbx";

        static GameObject Scarab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScarabPath);
            Assert.IsNotNull(prefab, $"{ScarabPath} is missing");
            return prefab;
        }

        [Test]
        public void Scarab_DrawsItsModel_NeverTheSparrowItUsedToWrap()
        {
            var prefab = Scarab();
            Assert.IsNull(prefab.GetComponentInChildren<ScarabHullBuilder>(true),
                "Scarab.prefab still carries the procedural ScarabHullBuilder - it would draw a second hull over the model");
            Assert.IsNull(ToyModelBuilder.HiddenLegacyModelRoot(prefab.transform),
                "Scarab.prefab still declares a hidden legacy model root");

            var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(1, skinned.Length, "the Scarab draws exactly one skinned hull");
            var mesh = skinned[0].sharedMesh;
            Assert.IsNotNull(mesh, "the Scarab's skinned hull has no mesh");
            Assert.AreEqual(ScarabModelPath, AssetDatabase.GetAssetPath(mesh), "the Scarab's hull mesh is not Scarab.fbx's");
            var shapes = Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToList();
            CollectionAssert.AreEquivalent(new[] { "Charge", "Mass", "Space", "Time" }, shapes,
                "the hull's element morphs are its blend shapes, by name");

            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh
                    : r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
                if (m) Assert.AreNotEqual(SparrowModelPath, AssetDatabase.GetAssetPath(m),
                    $"'{r.name}' on Scarab.prefab draws a Sparrow mesh");
            }
        }

        [Test]
        public void ToyHarvester_ShowsTheScarabModel()
        {
            var prefab = Scarab();
            Assert.IsTrue(VesselModelBuilder.TryBuild(prefab.transform, 1f, Color.white, out var model),
                "the Scarab harvested as NOTHING - the model was not emitted");
            try
            {
                var meshes = model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Where(m => m).ToList();
                Assert.IsTrue(meshes.Any(m => AssetDatabase.GetAssetPath(m) == ScarabModelPath),
                    "the mini Scarab does not draw Scarab.fbx");
                Assert.IsFalse(meshes.Any(m => AssetDatabase.GetAssetPath(m) == SparrowModelPath),
                    "the mini Scarab draws a Sparrow mesh");
            }
            finally
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m && !AssetDatabase.Contains(m)) Object.DestroyImmediate(m);
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void ScarabHullBuilder_StillAnswersTheHull_WithItsFormsParts()
        {
            // The retired procedural hull stays in the tree until the model is accepted (SCARAB.md
            // section 3.0); while it does, its preview seam must still describe what it would build.
            var go = new GameObject("ScarabHullBuilderProbe");
            try
            {
                var builder = go.AddComponent<ScarabHullBuilder>();
                var pieces = new List<ProceduralHullPiece>();
                ((IProceduralHullSource)builder).BuildPreviewPieces(pieces);
                var parts = ScarabHullForm.Generate(builder.CollectSettings());

                Assert.AreEqual(parts.Count, pieces.Count, "piece count differs from the runtime part list");
                for (int i = 0; i < parts.Count; i++)
                {
                    Assert.AreEqual(parts[i].Name, pieces[i].Name);
                    Assert.AreEqual(parts[i].Verts.Count, pieces[i].Vertices.Length, $"{parts[i].Name}: vertex count");
                    Assert.AreEqual(2, pieces[i].Submeshes.Length, $"{parts[i].Name}: chassis + shell slots");
                    // Same seating rule as EmitParts: the Core stays in hull space, children re-seat on their pivot.
                    Vector3 expectedSeat = i == 0 ? Vector3.zero : parts[i].Pivot;
                    Assert.AreEqual(expectedSeat, pieces[i].LocalPosition, $"{parts[i].Name}: seat");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
