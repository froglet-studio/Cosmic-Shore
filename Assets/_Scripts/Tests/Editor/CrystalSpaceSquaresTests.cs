#if UNITY_EDITOR
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The omni crystal's Space squares (<see cref="CrystalSpaceSquares"/>, Omni Shepard Lab round 9): the frame
    /// weights play as Blender plays them, the keys land on the body as blend shapes that move exactly the square
    /// plates, and the generator's wiring (<c>Tools/Build/author_omni_crystal_triangles.py</c>) puts the driver
    /// on the body with the baked asset.
    /// </summary>
    public class CrystalSpaceSquaresTests
    {
        const string SquaresPath = "Assets/_SO_Assets/Environment/OmniSpaceSquares.asset";
        const string OmniPrefabPath = "Assets/_Prefabs/Environment/Crystal.prefab";
        const string BodyFbxPath = "Assets/_Models/OmniCrystalExport1_8-21-25.fbx";

        // ------------------------------------------------------------------ playback

        [Test]
        public void Weights_PlayTheArtFilesFrames_AndHoldAcrossASnap()
        {
            // Two keys, four frames at 2 fps: key 0 rises 0 -> 1 then snaps to 0 while key 1 rises.
            var so = MakeSquares(new[] { Vector3.zero }, 1f, new[] { ("A", -1, new[] { Vector3.up }), ("B", -1, new[] { Vector3.up }) },
                                 new[] { 0f, 0f, 0.5f, 0f, 1f, 0f, 0f, 0.5f }, fps: 2f);
            try
            {
                Assert.AreEqual(4, so.FrameCount);
                Assert.AreEqual(2f, so.LoopSeconds, 1e-6f, "four frames at 2 fps is a 2 s loop");
                var w = new float[2];

                so.WeightsAt(0.5f, w);   // frame 1 exactly
                Assert.AreEqual(0.5f, w[0], 1e-6f);
                so.WeightsAt(0.75f, w);  // between frames 1 and 2
                Assert.AreEqual(0.75f, w[0], 1e-6f, "between frames the weights interpolate");

                so.WeightsAt(1.25f, w);  // between frames 2 (A = 1) and 3 (A = 0): a snap
                Assert.AreEqual(1f, w[0], 1e-6f, "across a snap the frame's values hold - a blend would sweep the squares across the body");
                Assert.AreEqual(0f, w[1], 1e-6f);

                so.WeightsAt(2.5f, w);   // wraps to frame 1
                Assert.AreEqual(0.5f, w[0], 1e-6f, "the loop wraps");
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        // ------------------------------------------------------------------ the blend shapes

        [Test]
        public void Shapes_MoveExactlyTheAnchoredVertices_AndTurnTheirNormals()
        {
            // A unit square plate in the XY plane, its authored normals facing -Z, and one still vertex far away.
            // The key folds the plate a quarter turn about its bottom edge - as a linear move of its two top
            // corners, the way Blender stores a key.
            var mesh = new Mesh { name = "plate" };
            mesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0), new Vector3(5, 5, 5) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.up };
            // Anchors written in import space: x negated, as the generator writes them.
            var anchors = new[] { new Vector3(0, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 1, 0), new Vector3(0, 1, 0) };
            var deltas = new[] { Vector3.zero, Vector3.zero, new Vector3(0, -1, -1), new Vector3(0, -1, -1) };
            var so = MakeSquares(anchors, Mathf.Sqrt(75f), new[] { ("Turn", -1, deltas) }, new[] { 0f, 1f }, fps: 1f);
            try
            {
                Assert.IsTrue(CrystalSpaceSquaresBaker.TryAddShapes(mesh, so, out var problem), problem);
                Assert.AreEqual(1, mesh.blendShapeCount);
                Assert.AreEqual(4, mesh.GetBlendShapeFrameCount(0), "normals need in-between frames: a turning plate's normal is not linear in its weight");

                int last = mesh.GetBlendShapeFrameCount(0) - 1;
                Assert.AreEqual(100f, mesh.GetBlendShapeFrameWeight(0, last));
                var dv = new Vector3[5];
                var dn = new Vector3[5];
                mesh.GetBlendShapeFrameVertices(0, last, dv, dn, null);
                // The mesh is the mirror of the anchors (x unflipped), so the baker matched it with x flipped back.
                Assert.That(Vector3.Distance(dv[2], new Vector3(0, -1, -1)), Is.LessThan(1e-6f), "an anchored corner moves by its key's offset");
                Assert.AreEqual(Vector3.zero, dv[0], "an anchored corner whose offset is zero holds");
                Assert.AreEqual(Vector3.zero, dv[4], "a vertex at no anchor holds still");
                Assert.AreEqual(Vector3.zero, dn[4]);
                // At weight 1 the plate lies in y = 0 (corners (0,0,0) (1,0,0) (1,0,-1) (0,0,-1)): its face turned
                // from +Z to +Y, so the authored -Z normal turns to -Y.
                var turned = (Vector3.back + dn[0]).normalized;
                Assert.That(Vector3.Distance(turned, Vector3.down), Is.LessThan(1e-4f), $"the normal turns with its face (got {turned})");

                mesh.GetBlendShapeFrameVertices(0, 1, dv, dn, null);
                Assert.AreEqual(50f, mesh.GetBlendShapeFrameWeight(0, 1));
                Assert.That(Vector3.Distance(dv[2], new Vector3(0, -0.5f, -0.5f)), Is.LessThan(1e-6f), "positions are linear in the weight, as Blender's are");
            }
            finally
            {
                Object.DestroyImmediate(so);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Shapes_RefuseABodyTheyWereNotBakedOn()
        {
            var mesh = new Mesh { name = "elsewhere" };
            mesh.vertices = new[] { new Vector3(3, 0, 0), new Vector3(0, 3, 0), new Vector3(0, 0, 3) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            var so = MakeSquares(new[] { new Vector3(1, 1, 1) }, 3f, new[] { ("K", -1, new[] { Vector3.up }) }, new[] { 0f, 1f }, fps: 1f);
            try
            {
                Assert.IsFalse(CrystalSpaceSquaresBaker.TryAddShapes(mesh, so, out var problem));
                StringAssert.Contains("anchors", problem);
                Assert.AreEqual(0, mesh.blendShapeCount, "a refused mesh is left untouched");
            }
            finally
            {
                Object.DestroyImmediate(so);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ShippedSquares_LandOnTheShippedBody()
        {
            var squares = AssetDatabase.LoadAssetAtPath<CrystalSpaceSquaresSO>(SquaresPath);
            Assert.IsNotNull(squares, $"{SquaresPath} not found");
            Mesh source = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(BodyFbxPath))
                if (asset is Mesh m) source = m;
            Assert.IsNotNull(source, $"{BodyFbxPath} imports no mesh");
            var body = Object.Instantiate(source);
            try
            {
                Assert.IsTrue(CrystalSpaceSquaresBaker.TryAddShapes(body, squares, out var problem), problem);
                Assert.AreEqual(squares.Keys.Length, body.blendShapeCount);
                // The importer splits a corner by its faces' normals; welded back, the moving vertices are exactly
                // the square plates' corners - nothing on a pentagon, rhombus or triangle moves.
                var dv = new Vector3[body.vertexCount];
                var verts = body.vertices;
                var corners = new System.Collections.Generic.HashSet<Vector3Int>();
                for (int k = 0; k < body.blendShapeCount; k++)
                {
                    body.GetBlendShapeFrameVertices(k, body.GetBlendShapeFrameCount(k) - 1, dv, null, null);
                    for (int i = 0; i < dv.Length; i++)
                        if (dv[i] != Vector3.zero) corners.Add(Vector3Int.RoundToInt(verts[i] * 1e4f));
                }
                Assert.AreEqual(squares.Anchors.Length, corners.Count, "the moving vertices, welded, must be exactly the anchors");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void ShapedTwin_ResolvesBackToItsSource()
        {
            var source = new Mesh { name = "source" };
            var twin = Object.Instantiate(source);
            try
            {
                RhombusSkinBaker.RegisterDerivedTwin(twin, source);
                Assert.AreSame(source, RhombusSkinBaker.SourceOf(twin), "a fusion bake or a morph keyed by the FBX mesh must still find it");
            }
            finally
            {
                Object.DestroyImmediate(twin);
                Object.DestroyImmediate(source);
            }
        }

        // ------------------------------------------------------------------ the wiring

        [Test]
        public void ShippedSquares_AreTheArtFilesThreeKeys_OnAFourPointEightSecondLoop()
        {
            var squares = AssetDatabase.LoadAssetAtPath<CrystalSpaceSquaresSO>(SquaresPath);
            Assert.IsNotNull(squares, $"{SquaresPath} not found");
            Assert.AreEqual(3, squares.Keys.Length);
            Assert.AreEqual("SquareRotateAroundPentagon", squares.Keys[0].Name);
            Assert.AreEqual(-1, squares.Keys[0].StackedOn);
            Assert.AreEqual(-1, squares.Keys[1].StackedOn);
            Assert.AreEqual(1, squares.Keys[2].StackedOn, "the weave's second step is relative to its first");
            Assert.AreEqual(480, squares.Anchors.Length, "60 square plates x 8 corners");
            Assert.AreEqual(120, squares.FrameCount);
            Assert.AreEqual(4.8f, squares.LoopSeconds, 1e-4f);
        }

        [Test]
        public void OmniBody_CarriesTheSquaresDriver()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OmniPrefabPath);
            Assert.IsNotNull(prefab, $"{OmniPrefabPath} not found");
            var body = prefab.GetComponent<Crystal>().CrystalModels[0].model;
            Assert.IsTrue(body.TryGetComponent(out CrystalSpaceSquares driver), "the omni body (slot 0) carries no CrystalSpaceSquares");
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<CrystalSpaceSquaresSO>(SquaresPath),
                           new SerializedObject(driver).FindProperty("squares").objectReferenceValue);
        }

        // ------------------------------------------------------------------ helpers

        static CrystalSpaceSquaresSO MakeSquares(Vector3[] anchors, float radius, (string name, int stackedOn, Vector3[] deltas)[] keys, float[] weights, float fps)
        {
            var so = ScriptableObject.CreateInstance<CrystalSpaceSquaresSO>();
            var s = new SerializedObject(so);
            var a = s.FindProperty("anchors");
            a.arraySize = anchors.Length;
            for (int i = 0; i < anchors.Length; i++) a.GetArrayElementAtIndex(i).vector3Value = anchors[i];
            s.FindProperty("bodyRadius").floatValue = radius;
            var k = s.FindProperty("keys");
            k.arraySize = keys.Length;
            for (int i = 0; i < keys.Length; i++)
            {
                var key = k.GetArrayElementAtIndex(i);
                key.FindPropertyRelative("name").stringValue = keys[i].name;
                key.FindPropertyRelative("stackedOn").intValue = keys[i].stackedOn;
                var d = key.FindPropertyRelative("deltas");
                d.arraySize = keys[i].deltas.Length;
                for (int j = 0; j < keys[i].deltas.Length; j++) d.GetArrayElementAtIndex(j).vector3Value = keys[i].deltas[j];
            }
            var w = s.FindProperty("weights");
            w.arraySize = weights.Length;
            for (int i = 0; i < weights.Length; i++) w.GetArrayElementAtIndex(i).floatValue = weights[i];
            s.FindProperty("framesPerSecond").floatValue = fps;
            s.ApplyModifiedPropertiesWithoutUndo();
            return so;
        }
    }
}
#endif
