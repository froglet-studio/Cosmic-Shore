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
    /// off, the meshes the player builds split by face, the round-5 effects fit the data, and the Ark screen is open
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
        public void EveryCrystalButTheFirst_ArrivesFromTheCamerasSide_FollowingIt_AndStillLands()
        {
            var data = LoadData();
            var core = new OmniCombineCore(data);
            var straight = OmniCombineParams.LabDefault;
            straight.GameMotion = true;
            straight.SocketSpacing = 0.1;
            for (int trial = 0; trial < 8; trial++)
            {
                double cameraAzimuth = trial * 0.9 - 3, rate = 0.087;   // ~5 degrees a second, as the converter's camera circles
                var aimed = straight;
                core.FaceCamera(ref aimed, cameraAzimuth, rate, 0);
                Assert.AreEqual(0.0, aimed.ApproachTurn[OmniCombineCore.Space], "Space keeps its path");
                for (int k = 1; k < 4; k++)
                {
                    double t0 = OmniCombineCore.Departs(k, aimed);
                    core.CrystalFrame(k, t0, straight, out var idle, out _, out _, out _, out _);
                    core.CrystalFrame(k, t0, aimed, out var setsOff, out _, out _, out _, out _);
                    Assert.Less((setsOff - idle).Length, 1e-9, "a crystal sets off from exactly where it idles");
                    double tA = t0 + 0.97 * aimed.Flight;
                    // The player hands the play the live camera every frame; the approach follows it at any speed.
                    foreach (double actual in new[] { rate, 2 * rate, 0.0 })
                    {
                        var live = aimed;
                        live.CameraAzimuth = cameraAzimuth + actual * tA;
                        core.CrystalFrame(k, tA, live, out var arriving, out _, out _, out _, out _);
                        double off = OmniCombineCore.WrapAngle(System.Math.Atan2(arriving.X, arriving.Z) - live.CameraAzimuth);
                        Assert.Less(System.Math.Abs(off), 12 * System.Math.PI / 180, $"{OmniCombineCore.Order[k]} does not arrive from the camera's side");
                    }
                }
                var pos = Buffers(core);
                core.Pose(OmniCombineCore.MorphEnds(aimed), aimed, Camera, pos, new float[4]);
                for (int k = 0; k < 4; k++)
                {
                    var final = core.CrystalData(k).final;
                    for (int i = 0; i < pos[k].Length; i++)
                        Assert.AreEqual(final[i], pos[k][i], 1e-4f, $"{OmniCombineCore.Order[k]} does not land in its slots");
                }
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
        public void TheBuiltMeshesSplitByFace_SoEachFaceShadesFlat()
        {
            // The converter draws every crystal on the Fresnel body with one normal per FACE (the art data shares a slab's
            // corners across its faces; drawn welded, the plates would shade smooth). Since 2026-10-13 nothing is baked for
            // a discharge: the Charge light is the seam walk and the flux arcs, drawn on the CPU.
            var data = LoadData();
            var charge = data.crystals[OmniCombineCore.Charge];
            var source = OmniCombinePlayer.BuildFaceSplitSource(charge.basis, charge.tris, charge.plate, out var faces);
            var omniSource = OmniCombinePlayer.BuildFaceSplitSource(data.omni.verts, data.omni.tris, data.omni.plate, out var omniFaces);
            try
            {
                // 60 pentagonal slabs of 7 faces (5 sides + 2 caps): split by face, never welded across one.
                Assert.AreEqual(420, faces.Max() + 1, "Charge faces");
                Assert.AreEqual(724, omniFaces.Max() + 1, "omni faces (30x6 + 20x5 + 12x7 + 60x6)");
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(omniSource);
            }
        }

        [Test]
        public void RoundFive_TheOmnisSquaresFitTheirTurns_AndTheChargeLightReadsBothCrystals()
        {
            var data = LoadData();
            var core = new OmniCombineCore(data);
            Assert.IsTrue(OmniCombineMotion.TryFit(data, (float)core.OmniRadius, 1.1f, out var motion, out var problem), problem);
            var offsets = motion.ThickOffset;
            var turns = motion.Squares;
            var corners = motion.SquareCorners;
            Assert.AreEqual(60, turns.Length, "every square plate turns");
            Assert.IsTrue(corners.All(c => c.Length == 8), "a square plate has eight corners");
            // The thicker plates: each square, rhombus and pentagon grows about its own centre; the triangles do not.
            for (int i = 0; i < data.omni.plate.Length; i++)
            {
                bool moved = offsets[3 * i] != 0f || offsets[3 * i + 1] != 0f || offsets[3 * i + 2] != 0f;
                if (data.omni.plateOwner[data.omni.plate[i]] == OmniCombineCore.Mass) Assert.IsFalse(moved, $"triangle vertex {i} moved");
            }
            Assert.IsTrue(offsets.Any(x => x != 0f), "no plate grew");

            var charge = core.CrystalData(OmniCombineCore.Charge);
            var cv = Enumerable.Range(0, charge.basis.Length / 3).Select(i => new Vector3(charge.basis[3 * i], charge.basis[3 * i + 1], charge.basis[3 * i + 2])).ToArray();
            Assert.IsTrue(ChargeSites.TryBuild(cv, charge.tris, 1f, out var blooms, out problem), problem);
            Assert.AreEqual(ChargeSiteKind.Blooms, blooms.Kind);
            var ov = Enumerable.Range(0, data.omni.verts.Length / 3).Select(i => new Vector3(data.omni.verts[3 * i], data.omni.verts[3 * i + 1], data.omni.verts[3 * i + 2])).ToArray();
            Assert.IsTrue(ChargeSites.TryBuild(ov, data.omni.tris, 1.1f, out var pentagons, out problem), problem);
            Assert.AreEqual(ChargeSiteKind.Pentagons, pentagons.Kind);
        }

        [Test]
        public void TheConfigIsWiredAndDrawsInsideItsRanges()
        {
            var config = AssetDatabase.LoadAssetAtPath<OmniCombineConfigSO>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath}: run Tools/Build/author_elemental_converter.py");
            Assert.IsNotNull(config.CombineData);
            Assert.IsNotNull(config.ShellMaterial);
            Assert.IsNotNull(config.OmniMaterial);
            Assert.IsNotNull(config.Cadence, "the woken omni plays the game's cadence");
            Assert.IsNotNull(config.ChargeArcs, "the Charge light is the game's");
            Assert.IsNotNull(config.OmniArcsMaterial);
            Assert.IsNotNull(config.MassMaterial, "the Mass crystal draws with the game's depth-prism material");
            Assert.IsNotNull(config.OmniTrianglesMaterial);
            Assert.IsNotNull(config.OmniTrianglesMesh);
            Assert.IsTrue(config.OmniTrianglesMesh.isReadable, "the converter reads the triangles' plate centres");
            Assert.IsNotNull(config.DepthProfile);
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                var p = config.Draw(rng);
                Assert.IsTrue(p.GameMotion, "the converter's omni moves as the game's does");
                Assert.That(p.SocketSpacing, Is.InRange(0.08 - 1e-6, 0.12 + 1e-6), "every crystal but the first comes in with a bit of extra spacing");
                Assert.IsNull(p.ApproachTurn, "the approach is aimed at the camera when the crystals set off, not when the play is drawn");
                Assert.That(p.Flight, Is.InRange(4.4, 5.4));
                Assert.That(p.Stagger / p.Flight, Is.InRange(0.95 - 1e-6, 1.05 + 1e-6), "each crystal sets off as the last lands");
                Assert.That(p.Lead, Is.InRange(2.2, 3.2));
                Assert.That(p.Suck, Is.InRange(0.3, 0.4));
            }
        }

        [Test]
        public void TheArkScreenIsOpenAndCarriesTheConverter()
        {
            // Normalise CRLF first: the line-anchored needles below end in "\n", and a Windows
            // checkout (text=auto) delivers the scene with "\r\n", so neither would ever match there.
            var scene = System.IO.File.ReadAllText(ScenePath).Replace("\r\n", "\n");
            StringAssert.Contains("  disabledScreens: 03000000\n", scene, "ARK must be open (only PORT disabled)");
            var view = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Scripts/UI/Elements/ElementalConverterView.cs");
            Assert.IsNotNull(view);
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(view, out string guid, out long _));
            StringAssert.Contains($"m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}", scene, "the converter view is in Menu_Main");
            StringAssert.Contains("m_Name: ElementalConverter\n", scene);
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
