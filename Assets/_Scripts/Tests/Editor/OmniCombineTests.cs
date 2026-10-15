#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Elemental Converter's OMNI COMBINE (<see cref="OmniCombineCore"/>, <see cref="OmniCombinePlayer"/>,
    /// <see cref="ElementalConverterView"/>): the shipped data loads through JsonUtility, every crystal lands
    /// exactly on its share of the omni, a play is a pure function of its seed, waiting never sets anything
    /// off, the discharge bakes find real creases on the meshes the player builds, and the Ark screen is open
    /// and wired.
    ///
    /// Parity with the Omni Combine Lab itself is held out of editor, vertex for vertex, by
    /// <c>Tools/Build/omni_combine_parity/run.sh</c> (it needs the lab page and a browser).
    /// </summary>
    public class OmniCombineTests
    {
        const string DataPath = "Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json";
        const string ConfigPath = "Assets/_SO_Assets/Environment/OmniCombine/OmniCombineConfig.asset";
        const string ScenePath = "Assets/_Scenes/Menu_Main.unity";

        static OmniCombineData LoadData()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            Assert.IsNotNull(text, $"{DataPath} is missing: run Tools/Build/author_omni_combine_asset.py");
            var data = JsonUtility.FromJson<OmniCombineData>(text.text);
            Assert.IsNotNull(data);
            return data;
        }

        static float[][] Buffers(OmniCombineCore core)
        {
            var pos = new float[4][];
            for (int k = 0; k < 4; k++) pos[k] = new float[3 * core.VertexCount(k)];
            return pos;
        }

        static readonly D3 Camera = new D3(0.35, 0.42, 0.837).Unit;

        [Test]
        public void TheShippedDataLoadsThroughJsonUtility()
        {
            var data = LoadData();
            Assert.AreEqual(4, data.crystals.Length);
            CollectionAssert.AreEqual(OmniCombineCore.Order, data.order, "the designer's order");
            var core = new OmniCombineCore(data);
            Assert.AreEqual(480, core.VertexCount(OmniCombineCore.Space));
            Assert.AreEqual(600, core.VertexCount(OmniCombineCore.Charge));
            Assert.AreEqual(360, core.VertexCount(OmniCombineCore.Mass));
            Assert.AreEqual(240, core.VertexCount(OmniCombineCore.Time));
            Assert.AreEqual(960 * 3, data.omni.verts.Length);
            Assert.Greater(data.crystals[OmniCombineCore.Space].idle.keys.Length, 0, "the Space crystal's idle spin keys");
            Assert.Greater(data.crystals[OmniCombineCore.Mass].meshScale, 0f, "the Mass crystal is the game's MassCrystalPrisms");
            Assert.AreEqual(9, data.omniTriangleRotation.Length);
        }

        [Test]
        public void TheGamesTrianglePrismsSitOnTheOmnisTriangles()
        {
            // The converter draws OmniCrystalTriangles.asset turned by the data's rotation; with its baked thickness
            // taken back out (the inverse of the shader's _Thickness term, about the material's top face), every
            // corner must land on a corner of the omni's triangle plates (the art file's omni), through the very
            // quaternion the player builds. That thin prism is what the Mass crystal morphs into before hand-off.
            var data = LoadData();
            var config = AssetDatabase.LoadAssetAtPath<OmniCombineConfigSO>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath}: run Tools/Build/author_elemental_converter.py");
            var mesh = config.OmniTrianglesMesh;
            Assert.IsNotNull(mesh);
            float thickness = data.depth.omniThickness, top = config.OmniTrianglesMaterial.GetFloat("_TopHeight");
            Assert.Greater(thickness, 0f);
            var plates = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(2, plates);
            var vertices = mesh.vertices;
            var turn = OmniCombinePlayer.TriangleRotation(data.omniTriangleRotation);
            var omni = data.omni.verts;
            float worst = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                var centre = (Vector3)plates[i];
                var dir = centre.normalized;
                float s = Vector3.Dot(vertices[i] - centre, dir);
                float thin = (s + top * (thickness - 1f)) / thickness;
                var p = turn * (vertices[i] - dir * ((thin - top) * (thickness - 1f)));
                float nearest = float.MaxValue;
                for (int j = 0; j < omni.Length; j += 3)
                {
                    if (data.omni.plateOwner[data.omni.plate[j / 3]] != OmniCombineCore.Mass) continue;
                    nearest = Mathf.Min(nearest, Vector3.Distance(p, new Vector3(omni[j], omni[j + 1], omni[j + 2])));
                }
                worst = Mathf.Max(worst, nearest);
            }
            Assert.Less(worst, 1e-3f, "the game's triangle prisms do not sit on the omni's triangles");
        }

        [Test]
        public void EveryCrystalLandsOnItsShareOfTheOmni()
        {
            var data = LoadData();
            var core = new OmniCombineCore(data);
            var p = OmniCombineParams.LabDefault;
            var pos = Buffers(core);
            core.Pose(OmniCombineCore.MorphEnds(p), p, Camera, pos, new float[4]);
            var omni = data.omni.verts;
            for (int k = 0; k < 4; k++)
            {
                var final = data.crystals[k].final;
                float worstFinal = 0f, worstOmni = 0f;
                for (int i = 0; i < pos[k].Length / 3; i++)
                {
                    var v = new Vector3(pos[k][3 * i], pos[k][3 * i + 1], pos[k][3 * i + 2]);
                    worstFinal = Mathf.Max(worstFinal, Vector3.Distance(v, new Vector3(final[3 * i], final[3 * i + 1], final[3 * i + 2])));
                    float nearest = float.MaxValue;
                    for (int j = 0; j < omni.Length; j += 3)
                        nearest = Mathf.Min(nearest, Vector3.Distance(v, new Vector3(omni[j], omni[j + 1], omni[j + 2])));
                    worstOmni = Mathf.Max(worstOmni, nearest);
                }
                Assert.Less(worstFinal, 1e-4f, $"{OmniCombineCore.Order[k]} does not reach its morph target");
                // Space and Time land on omni VERTICES (Time's rigid lock-in is 0.0068 off at worst, the art file's own
                // snap). Charge's and Mass's wedges put tips at a face's centre and backs along its sides, so they are
                // held by Tools/Build/omni_combine_lab_assets.py's tiling gate instead.
                if (k == OmniCombineCore.Space || k == OmniCombineCore.Time)
                    Assert.Less(worstOmni, 0.012f, $"{OmniCombineCore.Order[k]} lands off the omni");
            }
        }

        [Test]
        public void APlayIsAFunctionOfItsSeed()
        {
            var core = new OmniCombineCore(LoadData());
            var a = OmniCombineParams.LabDefault;
            var b = a;
            b.Seed = 12345;
            var p1 = Buffers(core);
            var p2 = Buffers(core);
            core.Pose(4.0, a, Camera, p1, new float[4]);
            core.Pose(4.0, b, Camera, p2, new float[4]);
            core.Pose(4.0, a, Camera, p2, new float[4]);
            for (int k = 0; k < 4; k++) CollectionAssert.AreEqual(p1[k], p2[k], $"{OmniCombineCore.Order[k]}: same seed, same pose");
            core.Pose(0.5, b, Camera, p2, new float[4]);
            core.Pose(0.5, a, Camera, p1, new float[4]);
            Assert.IsFalse(p1[0].SequenceEqual(p2[0]), "a different seed must tumble the crystals differently");
        }

        [Test]
        public void WaitingNeverSetsACrystalOff()
        {
            var core = new OmniCombineCore(LoadData());
            var p = OmniCombineParams.LabDefault;
            p.Lead = 1e9;
            var early = Buffers(core);
            var late = Buffers(core);
            var fx = new float[4];
            core.Pose(0.0, p, Camera, early, fx);
            core.Pose(600.0, p, Camera, late, fx);
            for (int k = 0; k < 4; k++)
            {
                Assert.AreEqual(1f, fx[k], 1e-6f, $"{OmniCombineCore.Order[k]}'s idle effect must still be running");
                Vector3 c0 = Centre(early[k]), c1 = Centre(late[k]);
                Assert.Less(Vector3.Distance(c0, c1), 0.05f * (float)core.OmniRadius, $"{OmniCombineCore.Order[k]} drifted while waiting");
            }
        }

        [Test]
        public void ConvertingAfterALongWaitIsContinuous()
        {
            // Convert sets the lead to "now", so the frame before and after the press must be the same pose, and
            // Charge's tumble (only the turn modulo a whole turn unwinds) may not lurch.
            var core = new OmniCombineCore(LoadData());
            var waiting = OmniCombineParams.LabDefault;
            waiting.Lead = 1e9;
            var go = waiting;
            go.Lead = 300.3;
            var a = Buffers(core);
            var b = Buffers(core);
            core.Pose(300.0, waiting, Camera, a, new float[4]);
            core.Pose(300.0, go, Camera, b, new float[4]);
            for (int k = 0; k < 4; k++)
                for (int i = 0; i < a[k].Length; i++)
                    Assert.AreEqual(a[k][i], b[k][i], 1e-4f, $"{OmniCombineCore.Order[k]} jumps on CONVERT");
        }

        [Test]
        public void BothFlipRigsMatchTheGamesPlan()
        {
            var core = new OmniCombineCore(LoadData());
            foreach (bool omni in new[] { false, true })
            {
                Assert.AreEqual(12, core.StartVertexCount(omni), omni ? "omni" : "Time crystal");
                for (int s = 0; s < 12; s++)
                    CollectionAssert.AreEqual(new[] { 5, 5, 10, 5, 5 }, core.RingSizes(omni, s));
            }
        }

        [Test]
        public void TheDischargeBakesFindRealCreasesOnTheBuiltMeshes()
        {
            var data = LoadData();
            var charge = data.crystals[OmniCombineCore.Charge];
            var source = OmniCombinePlayer.BuildFaceSplitSource(charge.basis, charge.tris, charge.plate, out var faces);
            var baked = CrystalEdgeArcMeshBaker.Bake(source, 0);
            var omniSource = OmniCombinePlayer.BuildFaceSplitSource(data.omni.verts, data.omni.tris, data.omni.plate, out var omniFaces);
            var pentagons = CrystalEdgeArcMeshBaker.Bake(omniSource, 10);
            try
            {
                // 60 pentagonal slabs of 7 faces (5 sides + 2 caps): split by face, never welded across one.
                Assert.AreEqual(420, faces.Max() + 1, "Charge faces");
                Assert.AreEqual(724, omniFaces.Max() + 1, "omni faces (30x6 + 20x5 + 12x7 + 60x6)");
                Assert.IsNotNull(baked, "the Charge crystal did not bake");
                Assert.AreEqual(charge.tris.Length, baked.vertexCount, "one baked vertex per triangle corner, in triangle order");
                var heights = new System.Collections.Generic.List<Vector3>();
                baked.GetUVs(2, heights);
                int creases = heights.Sum(h => (h.x > 0 ? 1 : 0) + (h.y > 0 ? 1 : 0) + (h.z > 0 ? 1 : 0));
                int diagonals = heights.Sum(h => (h.x < 0 ? 1 : 0) + (h.y < 0 ? 1 : 0) + (h.z < 0 ? 1 : 0));
                Assert.Greater(creases, 0, "no crease: the discharge would never draw");
                Assert.Greater(diagonals, 0, "no triangulation diagonal: faces were not grouped");
                Assert.IsNotNull(pentagons, "the omni's pentagons did not bake");
                Assert.AreEqual(12 * 16 * 3, pentagons.vertexCount, "the twelve pentagonal prisms (16 triangles each)");
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(omniSource);
                if (baked) Object.DestroyImmediate(baked);
                if (pentagons) Object.DestroyImmediate(pentagons);
            }
        }

        [Test]
        public void TheConfigIsWiredAndDrawsInsideItsRanges()
        {
            var config = AssetDatabase.LoadAssetAtPath<OmniCombineConfigSO>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath}: run Tools/Build/author_elemental_converter.py");
            Assert.IsNotNull(config.CombineData);
            Assert.IsNotNull(config.ChargeMaterial);
            Assert.IsNotNull(config.ShellMaterial);
            Assert.IsNotNull(config.OmniMaterial);
            Assert.IsNotNull(config.OmniChargeEdgesMaterial);
            Assert.IsNotNull(config.MassMaterial, "the Mass crystal draws with the game's depth-prism material");
            Assert.IsNotNull(config.OmniTrianglesMaterial);
            Assert.IsNotNull(config.OmniTrianglesMesh);
            Assert.IsTrue(config.OmniTrianglesMesh.isReadable, "the converter reads the triangles' plate centres");
            Assert.IsNotNull(config.DepthProfile);
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                var p = config.Draw(rng);
                Assert.That(p.Flight, Is.InRange(4.4, 5.4));
                Assert.That(p.Stagger / p.Flight, Is.InRange(0.95 - 1e-6, 1.05 + 1e-6), "each crystal sets off as the last lands");
                Assert.That(p.Lead, Is.InRange(2.2, 3.2));
                Assert.That(p.Suck, Is.InRange(0.3, 0.4));
            }
        }

        [Test]
        public void TheArkScreenIsOpenAndCarriesTheConverter()
        {
            var scene = System.IO.File.ReadAllText(ScenePath);
            StringAssert.Contains("  disabledScreens: 03000000\n", scene, "ARK must be open (only PORT disabled)");
            var view = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Scripts/UI/Elements/ElementalConverterView.cs");
            Assert.IsNotNull(view);
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(view, out string guid, out long _));
            StringAssert.Contains($"m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}", scene, "the converter view is in Menu_Main");
            StringAssert.Contains("m_Name: ElementalConverter\n", scene);
        }

        [TestCase(0, "00:00")]
        [TestCase(11, "00:11")]
        [TestCase(65, "01:05")]
        [TestCase(-3, "00:00")]
        public void TheConverterClockReadsMinutesAndSeconds(int seconds, string expected)
        {
            Assert.AreEqual(expected, ElementalConverterView.Clock(seconds));
        }

        static Vector3 Centre(float[] p)
        {
            var c = Vector3.zero;
            for (int i = 0; i < p.Length; i += 3) c += new Vector3(p[i], p[i + 1], p[i + 2]);
            return c / (p.Length / 3);
        }
    }
}
#endif
