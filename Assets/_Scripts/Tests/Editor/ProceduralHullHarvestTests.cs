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
    /// shows. The Scarab's hull is generated at Awake and its inherited Sparrow model is hidden
    /// at Awake, so on the asset the real hull is an empty MeshFilter beside a still-enabled
    /// Sparrow - which is how the codex baked the Scarab as a Sparrow, byte for byte, and how the
    /// toybox's mini Scarab was a Sparrow too. Holds: the toy harvester skips the hidden legacy
    /// model and emits the procedural hull's own pieces, and the Scarab answers
    /// <see cref="IProceduralHullSource"/> off the asset with the same parts its runtime build emits.
    /// </summary>
    public class ProceduralHullHarvestTests
    {
        const string ScarabPath = "Assets/_Prefabs/Spacevessels/Scarab.prefab";

        static GameObject Scarab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScarabPath);
            Assert.IsNotNull(prefab, $"{ScarabPath} is missing");
            return prefab;
        }

        [Test]
        public void Scarab_AnswersTheHullOffTheAsset_WithItsRuntimeParts()
        {
            var builder = Scarab().GetComponentInChildren<ScarabHullBuilder>(true);
            Assert.IsNotNull(builder, "Scarab.prefab carries no ScarabHullBuilder");
            Assert.IsNotNull(builder.HiddenLegacyModelRoot, "the Scarab's legacy model root is unwired");

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

        [Test]
        public void ToyHarvester_ShowsTheProceduralHull_NeverTheHiddenLegacyModel()
        {
            var prefab = Scarab();
            var hiddenRoot = ToyModelBuilder.HiddenLegacyModelRoot(prefab.transform);
            Assert.IsNotNull(hiddenRoot, "no hidden legacy root resolved for the Scarab");
            var hiddenMeshes = new HashSet<Mesh>();
            foreach (var smr in hiddenRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh) hiddenMeshes.Add(smr.sharedMesh);
            foreach (var mf in hiddenRoot.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh) hiddenMeshes.Add(mf.sharedMesh);
            Assert.IsNotEmpty(hiddenMeshes, "the legacy root carries no meshes - the trap this test guards is gone; re-check");

            Assert.IsTrue(VesselModelBuilder.TryBuild(prefab.transform, 1f, Color.white, out var model),
                "the Scarab harvested as NOTHING - the procedural hull was not emitted");
            try
            {
                var filters = model.GetComponentsInChildren<MeshFilter>(true);
                var names = filters.Select(f => f.name).ToList();
                Assert.IsTrue(names.Contains("Core"), $"no Core piece in the mini hull: {string.Join(", ", names)}");
                Assert.IsTrue(names.Contains("horn"), $"no horn piece in the mini hull: {string.Join(", ", names)}");
                foreach (var f in filters)
                    Assert.IsFalse(hiddenMeshes.Contains(f.sharedMesh),
                        $"the mini Scarab draws '{f.name}' with a mesh from the HIDDEN legacy model - a Sparrow wearing a Scarab's name");
                Assert.IsNotNull(model.GetComponent<ToyMintedMeshes>(), "minted procedural meshes have no owner");
            }
            finally
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m && !AssetDatabase.Contains(m)) Object.DestroyImmediate(m);
                Object.DestroyImmediate(model);
            }
        }
    }
}
#endif
