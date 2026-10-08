#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the black hole's WARP and its config (Docs/BLACK_HOLE.md,
    /// Docs/PRISM_ANIMATION.md §4.7.4). Its failure modes are SILENT: a graph that lost the node
    /// renders every prism exactly as before and nothing logs; a bank length that drifts between
    /// the C#, the HLSL and the job's well capacity leaves the tail of the bank unread; a warp
    /// node spliced on the wrong side of the cradle displaces the cradle from LAST and breaks the
    /// Urchin's ride feel with nothing to say so. Every check runs from assets alone — no play
    /// mode — and shares its constants with the validator so the two cannot disagree.
    /// </summary>
    public class BlackHoleTests
    {
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismGravityWarp.hlsl";
        const string FunctionName = "PrismGravityWarpDeform";
        const string CradleFunctionName = "PrismCradleDeform";
        const string ConfigAssetPath = "Assets/Resources/" + BlackHoleRegistry.ConfigResourcePath + ".asset";
        const string TestScenePath = "Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity";

        static readonly string[] LiveGraphs =
        {
            "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
            "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
        };

        // Slot ids in the order the HLSL declares its parameters (inputs first, then outputs):
        // Position 0, Normal 1, OutPosition 2, OutNormal 3 — on the warp AND on the cradle.
        const int SlotPosition = 0;
        const int SlotNormal = 1;
        const int SlotOutPosition = 2;
        const int SlotOutNormal = 3;

        static string[] Blocks(string path)
        {
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            return text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        static string BlockObjectId(string[] blocks, string serializedDescriptor)
        {
            var block = blocks.FirstOrDefault(b =>
                b.Contains($"\"m_SerializedDescriptor\": \"{serializedDescriptor}\""));
            Assert.IsNotNull(block, $"no {serializedDescriptor} block node");
            var m = Regex.Match(block, "\"m_ObjectId\":\\s*\"([0-9a-f]+)\"");
            Assert.IsTrue(m.Success, $"{serializedDescriptor} block has no m_ObjectId");
            return m.Groups[1].Value;
        }

        static BlackHoleConfigSO LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BlackHoleConfigSO>(ConfigAssetPath);
            // No asset is a legal state (the SO's defaults apply) — but the defaults must then be sane.
            return config != null ? config : ScriptableObject.CreateInstance<BlackHoleConfigSO>();
        }

        [Test]
        public void WarpHlsl_ExistsDeclaresTheFunctionAndTheFileScopeBank()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing.");
            string hlsl = File.ReadAllText(HlslPath);

            Assert.IsTrue(hlsl.Contains("void PrismGravityWarpDeform_float(float3 Position, float3 Normal,"),
                "PrismGravityWarp.hlsl no longer declares PrismGravityWarpDeform_float(Position, Normal, ...) — every " +
                "wired graph would fail to compile, or bind its slots to the wrong parameters.");

            foreach (var decl in new[]
                     {
                         "float4 _PrismGravityWarpCentre[PRISM_GRAVITY_WARP_SLOTS]",
                         "float4 _PrismGravityWarpWeight[PRISM_GRAVITY_WARP_SLOTS]",
                         "float4 _PrismGravityWarpParams",
                     })
            {
                Assert.IsTrue(hlsl.Contains(decl), $"PrismGravityWarp.hlsl no longer declares `{decl}`.");
            }
            Assert.IsFalse(hlsl.Contains("CBUFFER_START"),
                "PrismGravityWarp.hlsl declares a CBUFFER — the bank must stay a file-scope global or SRP batching breaks.");

            var slots = Regex.Match(hlsl, @"#define PRISM_GRAVITY_WARP_SLOTS (\d+)");
            Assert.IsTrue(slots.Success, "PRISM_GRAVITY_WARP_SLOTS is not #defined in PrismGravityWarp.hlsl.");
            int hlslSlots = int.Parse(slots.Groups[1].Value);
            Assert.AreEqual(BlackHoleWarp.Slots, hlslSlots,
                "BlackHoleWarp.Slots (C#) and PRISM_GRAVITY_WARP_SLOTS (HLSL) disagree — the publisher would write " +
                "a bank the shader reads at a different length. Change both together.");
            Assert.AreEqual(BlackHolePhysics.NativeWells.Capacity, hlslSlots,
                "BlackHolePhysics.NativeWells.Capacity and PRISM_GRAVITY_WARP_SLOTS disagree — a hole the field pulls " +
                "toward must be one the warp can bend around. Change all three together.");

            string meta = File.ReadAllText(HlslPath + ".meta");
            var guid = Regex.Match(meta, @"guid:\s*([0-9a-f]{32})");
            Assert.IsTrue(guid.Success, "PrismGravityWarp.hlsl.meta carries no guid.");
            Assert.AreEqual(PrismClockWiringValidator.GravityWarpHlslGuid, guid.Groups[1].Value,
                "PrismClockWiringValidator.GravityWarpHlslGuid does not match PrismGravityWarp.hlsl.meta.");
        }

        [Test]
        public void EveryLiveGraph_CarriesTheWarpImmediatelyBeforeTheCradle()
        {
            foreach (var graphPath in LiveGraphs)
            {
                Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
                var blocks = Blocks(graphPath);

                Assert.IsTrue(PrismClockWiringValidator.TryFindCustomFunctionNodeId(blocks, FunctionName, out var warpId),
                    $"{graphPath} has no {FunctionName} Custom Function node — run Tools/Shaders/wire_prism_gravity_warp.py.");
                Assert.IsTrue(PrismClockWiringValidator.TryFindCustomFunctionNodeId(blocks, CradleFunctionName, out var cradleId),
                    $"{graphPath} has no {CradleFunctionName} node — the warp's anchor is gone (run wire_prism_cradle.py).");

                var warpBlock = blocks.First(b => b.Contains($"\"m_FunctionName\": \"{FunctionName}\""));
                Assert.IsTrue(warpBlock.Contains($"\"m_FunctionSource\": \"{PrismClockWiringValidator.GravityWarpHlslGuid}\""),
                    $"{graphPath}: {FunctionName} does not source PrismGravityWarp.hlsl.");

                var edges = PrismClockWiringValidator.ParseEdges(blocks[0]);
                string posBlock = BlockObjectId(blocks, "VertexDescription.Position");
                string nrmBlock = BlockObjectId(blocks, "VertexDescription.Normal");

                // The CRADLE is still LAST: it feeds the blocks directly ...
                var posFeeders = edges.Where(e => e.inNode == posBlock).ToList();
                var nrmFeeders = edges.Where(e => e.inNode == nrmBlock).ToList();
                Assert.AreEqual(1, posFeeders.Count, $"{graphPath}: VertexDescription.Position must have exactly one feeder.");
                Assert.AreEqual(1, nrmFeeders.Count, $"{graphPath}: VertexDescription.Normal must have exactly one feeder.");
                Assert.AreEqual(cradleId, posFeeders[0].outNode,
                    $"{graphPath}: VertexDescription.Position is not fed by the cradle — the warp displaced it from LAST.");
                Assert.AreEqual(cradleId, nrmFeeders[0].outNode,
                    $"{graphPath}: VertexDescription.Normal is not fed by the cradle — the warp displaced it from LAST.");

                // ... and the warp feeds the cradle: the cradle's inputs are the warp's outputs.
                var cradlePos = edges.FirstOrDefault(e => e.inNode == cradleId && e.inSlot == SlotPosition);
                var cradleNrm = edges.FirstOrDefault(e => e.inNode == cradleId && e.inSlot == SlotNormal);
                Assert.AreEqual((warpId, SlotOutPosition), (cradlePos.outNode, cradlePos.outSlot),
                    $"{graphPath}: {CradleFunctionName}.Position is not fed by {FunctionName}.OutPosition — the warp must sit immediately before the cradle.");
                Assert.AreEqual((warpId, SlotOutNormal), (cradleNrm.outNode, cradleNrm.outSlot),
                    $"{graphPath}: {CradleFunctionName}.Normal is not fed by {FunctionName}.OutNormal — the warp must sit immediately before the cradle.");

                // The warp wraps the chain rather than replacing it: both inputs fed, not by itself,
                // and not by the cradle (which would be the splice on the wrong side).
                foreach (var (slot, label) in new[] { (SlotPosition, "Position"), (SlotNormal, "Normal") })
                {
                    var src = edges.FirstOrDefault(e => e.inNode == warpId && e.inSlot == slot);
                    Assert.IsNotNull(src.outNode, $"{graphPath}: {FunctionName}.{label} is unconnected — the vertex chain was dropped.");
                    Assert.AreNotEqual(warpId, src.outNode, $"{graphPath}: {FunctionName}.{label} is fed by itself.");
                    Assert.AreNotEqual(cradleId, src.outNode, $"{graphPath}: {FunctionName}.{label} is fed by the cradle — spliced on the wrong side.");
                }

                // The bank must NOT also exist as graph properties: a same-named property would
                // shadow the file-scope declaration and read the per-material default (zero).
                foreach (var name in new[] { "_PrismGravityWarpCentre", "_PrismGravityWarpWeight", "_PrismGravityWarpParams" })
                {
                    Assert.IsFalse(blocks.Any(b => b.Contains("ShaderProperty") &&
                                                   (b.Contains($"\"m_DefaultReferenceName\": \"{name}\"") ||
                                                    b.Contains($"\"m_OverrideReferenceName\": \"{name}\""))),
                        $"{graphPath} declares {name} as a graph PROPERTY — the warp bank is a file-scope global in the HLSL, never a property.");
                }
            }
        }

        [Test]
        public void Specs_NameTheWarpOnBothLiveGraphs_AndNotOnSuction()
        {
            var byName = PrismClockWiringValidator.Specs.ToDictionary(s => s.GraphName);
            foreach (var graph in new[] { "BlockGraph", "ExplodingBlockGraph" })
            {
                Assert.IsTrue(byName[graph].CustomFunctions.Contains(FunctionName),
                    $"PrismClockWiringValidator.Specs[{graph}] no longer names {FunctionName} — the menu validator would print ALL PRESENT with the warp unwired.");
                // The validator's edge checks must say the same thing this test says about order.
                Assert.IsTrue(byName[graph].EdgeChecks.Any(e => e.InputFunction == CradleFunctionName && e.OutputFunction == FunctionName),
                    $"PrismClockWiringValidator.Specs[{graph}] does not assert the cradle is fed by the warp.");
                Assert.IsTrue(byName[graph].EdgeChecks.Any(e => e.InputFunction == FunctionName),
                    $"PrismClockWiringValidator.Specs[{graph}] does not assert the warp's own feeders.");
            }
            // Consumed mass is drawn by the implosion carrier, not warped — the same exclusion the cradle makes.
            Assert.IsFalse(byName["SuctionGraph"].CustomFunctions.Contains(FunctionName),
                "SuctionGraph must not carry the gravity warp: it renders mass being consumed.");
            Assert.AreEqual("PrismGravityWarp.hlsl", PrismClockWiringValidator.CustomFunctionSourceHint(FunctionName));
            Assert.AreEqual(PrismClockWiringValidator.GravityWarpHlslGuid,
                PrismClockWiringValidator.ExpectedCustomFunctionSourceGuid(FunctionName));
        }

        [Test]
        public void Config_IsSaneWhenAuthored()
        {
            var config = LoadConfig();
            Assert.IsTrue(config.IsSane, "BlackHoleConfig is not sane: the registry refuses every spawn and the shader treats the bank as OFF.");
            Assert.Greater(config.WarpReachMultiplier, 1f,
                "BlackHoleConfig.WarpReachMultiplier is 1 or less — the tide has no shell to fade across.");
            Assert.Greater(config.MaxTidalStretch, 1f,
                "BlackHoleConfig.MaxTidalStretch is 1 or less — the ceiling would forbid any stretch at all.");
            Assert.GreaterOrEqual(config.TidalResponseSeconds, 0f);
            Assert.That(config.Spin, Is.InRange(0f, 0.998f), "a* outside [0, 0.998] is not a black hole.");
            Assert.LessOrEqual(config.MaxBlackHoles, BlackHoleWarp.Slots,
                "BlackHoleConfig.MaxBlackHoles exceeds the warp bank — a hole past the bank pulls mass the shader cannot bend around.");

            // The derived numbers are monotone in strength: a stronger hole is bigger in every sense.
            Assert.Greater(config.HorizonRadius(20f), config.HorizonRadius(10f));
            Assert.Greater(config.GM(20f), config.GM(10f));
            Assert.GreaterOrEqual(config.InfluenceRadius(20f), config.InfluenceRadius(10f));
            Assert.Greater(config.InfluenceRadius(10f), config.HorizonRadius(10f),
                "the influence radius must enclose the horizon, or nothing can ever be admitted.");
            Assert.LessOrEqual(config.InfluenceRadius(100000f), config.MaxInfluenceRadius,
                "the influence radius is not capped — an operator's typo would query the whole arena every frame.");
        }

        [Test]
        public void Config_TidesAreTheTidalTensor()
        {
            // The coefficient the bank publishes is GM·τ², so the log-stretch is GM·τ²/d³: strongest
            // at the horizon, 8× weaker at twice the distance, and — because the horizon grows with
            // the mass — weaker at a big hole's horizon than at a small one's (Docs/BLACK_HOLE.md §5).
            var config = LoadConfig();
            float gm = config.GM(10f), rs = config.HorizonRadius(10f);
            float tau = config.TidalResponseSeconds;
            Assert.AreEqual(gm * tau * tau / (rs * rs * rs), config.TidalLogStretch(gm, rs), 1e-4f);
            if (tau > 0f)
            {
                float atHorizon = config.TidalLogStretch(gm, rs);
                Assert.AreEqual(atHorizon / 8f, config.TidalLogStretch(gm, 2f * rs), atHorizon * 1e-4f);
                Assert.Greater(config.TidalLogStretch(config.GM(10f), config.HorizonRadius(10f)),
                    config.TidalLogStretch(config.GM(40f), config.HorizonRadius(40f)),
                    "a bigger hole stretches harder at its own horizon — tides at the horizon scale as 1/M².");
            }
            Assert.Greater(config.MaxBodies, 0, "BlackHoleConfig.MaxBodies is 0 — no prism can ever be pulled.");
        }

        [Test]
        public void Lens_MaterialShaderAndHlslShipTogether()
        {
            const string shaderPath = "Assets/_Graphics/Materials/Graphs/BlackHoleLens.shader";
            const string lensHlslPath = "Assets/_Graphics/Materials/Graphs/BlackHoleLens.hlsl";
            Assert.IsTrue(File.Exists(shaderPath), $"{shaderPath} is missing.");
            Assert.IsTrue(File.Exists(lensHlslPath), $"{lensHlslPath} is missing.");

            string shader = File.ReadAllText(shaderPath);
            Assert.IsTrue(shader.Contains("Shader \"CosmicShore/BlackHoleLens\""), "the lens shader was renamed.");
            Assert.IsTrue(shader.Contains("#include \"BlackHoleLens.hlsl\""), "the lens shader no longer includes the traced HLSL.");
            Assert.IsTrue(shader.Contains("DeclareDepthTexture.hlsl") && shader.Contains("_BlackHoleSceneColor"),
                "the lens shader no longer reads the depth texture and BlackHoleLensPass's scene copy.");
            Assert.IsTrue(shader.Contains($"\"LightMode\" = \"{BlackHoleLensPass.LightModeName}\""),
                "the lens pass must carry LightMode BlackHoleLens — drawn by BlackHoleLensPass AFTER the transparents, " +
                "so the shards and particles behind the hole are in what it bends (URP's own passes would draw it before them).");
            Assert.IsFalse(shader.Contains("SampleSceneColor") || shader.Contains("DeclareOpaqueTexture"),
                "the lens reads URP's opaque copy again — taken before the transparents, it has no shards in it.");
            Assert.IsTrue(shader.Contains("Cull Front") && shader.Contains("ZTest Always"),
                "the lens draws the FAR side of its sphere with its own depth test — a camera-facing quad or a " +
                "hardware depth test cuts the lens off up close and loses it from inside.");

            string hlsl = File.ReadAllText(lensHlslPath);
            Assert.IsTrue(hlsl.Contains("void BlackHoleLensTrace("), "BlackHoleLens.hlsl lost its trace entry point.");

            // The material the lens loads at runtime must point at THIS shader, from Resources (so a
            // player build includes both).
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/" + BlackHoleLens.MaterialResourcePath + ".mat");
            Assert.IsNotNull(material, "Assets/Resources/BlackHoleLens.mat is missing — every hole falls back to a black sphere.");
            Assert.IsNotNull(material.shader, "BlackHoleLens.mat has no shader.");
            Assert.AreEqual("CosmicShore/BlackHoleLens", material.shader.name, "BlackHoleLens.mat points at the wrong shader.");
        }

        /// <summary>
        /// The lens shader COMPILES in this Editor — the check the text assertions above cannot make.
        /// The first lens shipped calling DecodeHDREnvironment without the include that declares it:
        /// the file read correctly, a mock-library compile passed, and every hole drew a magenta quad.
        /// </summary>
        [Test]
        public void Lens_ShaderCompilesAndTheLensIsDrawable()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/" + BlackHoleLens.MaterialResourcePath + ".mat");
            Assert.IsNotNull(material, "Assets/Resources/BlackHoleLens.mat is missing.");
            var errors = ShaderUtil.GetShaderMessages(material.shader)
                .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                .Select(m => $"{m.message} ({m.file}:{m.line}, {m.platform})")
                .ToArray();
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader),
                "CosmicShore/BlackHoleLens does not compile — every hole draws magenta:\n" + string.Join("\n", errors));
            Assert.IsTrue(BlackHoleLens.IsDrawable(material, out string reason), reason);
        }

        /// <summary>
        /// The lens mesh covers every ray through the lens and draws exactly its far side: every
        /// face lies at or outside the unit-diameter sphere (the lens scale is its diameter) and is
        /// wound outward, so the shader's Cull Front keeps the far side from outside AND inside.
        /// </summary>
        [Test]
        public void Lens_SphereCircumscribesTheLensAndFacesOutward()
        {
            var mesh = BlackHoleLens.LensSphere();
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            Assert.Greater(triangles.Length, 0, "the lens sphere has no triangles.");
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                var normal = Vector3.Cross(b - a, c - a).normalized;
                Assert.Greater(Vector3.Dot(normal, a + b + c), 0f,
                    $"lens face {i / 3} is wound inward — Cull Front would draw the near side.");
                Assert.GreaterOrEqual(Vector3.Dot(normal, a), 0.5f - 1e-5f,
                    $"lens face {i / 3} cuts inside the lens sphere — rays through that corner are never lensed.");
            }
        }

        [Test]
        public void Config_LensEnclosesTheShadowAndFadesBeforeItsEdge()
        {
            var config = LoadConfig();
            // The shadow is the photon-capture cross-section, b_c = (3√3/2) r_s ≈ 2.6 r_s; the bend
            // must still be exact (inside the fade start) well outside it.
            Assert.Greater(config.LensRadiusMultiplier * config.LensFadeStart, 2.6f * 2f,
                "the lens fades its bend out too close to the shadow — the Einstein ring would be flattened.");
            Assert.Less(config.LensFadeStart, 1f, "the bend must fade out before the lens edge, or the edge is a seam.");
        }

        /// <summary>
        /// There is no painted accretion disc (Docs/BLACK_HOLE.md §5.1): what orbits the hole is the
        /// real mass the gravity field moves. A synthetic disc was built, read in the editor as a
        /// disc slicing through the hole, and was removed — this keeps it from creeping back.
        /// </summary>
        [Test]
        public void Lens_HasNoPaintedAccretionDisc()
        {
            string shader = File.ReadAllText("Assets/_Graphics/Materials/Graphs/BlackHoleLens.shader");
            string hlsl = File.ReadAllText("Assets/_Graphics/Materials/Graphs/BlackHoleLens.hlsl");
            Assert.IsFalse(shader.Contains("_BHDisk") || hlsl.Contains("BlackHoleDiskEmission"),
                "the lens paints an accretion disc again — the hole is its shadow and the lensed scene only.");
        }

        /// <summary>
        /// A ray bent off the screen shows the scene's OWN skybox (BlackHoleSky), never URP's baked
        /// environment reflection — which is Unity's default sky until a scene's lighting is generated,
        /// and was drawn warped around a HyperSea hole, seamed against the real sky at the lens's rim.
        /// </summary>
        [Test]
        public void Lens_SkyIsTheScenesOwnSkyboxNotTheBakedReflection()
        {
            string shader = File.ReadAllText("Assets/_Graphics/Materials/Graphs/BlackHoleLens.shader");
            string hlsl = File.ReadAllText("Assets/_Graphics/Materials/Graphs/BlackHoleLens.hlsl");
            Assert.IsFalse(shader.Contains("_GlossyEnvironmentCubeMap") || shader.Contains("unity_SpecCube0"),
                "the lens samples URP's baked environment reflection again — the default sky until lighting is generated.");
            Assert.IsTrue(shader.Contains("_BlackHoleSky") && shader.Contains("BlackHoleSkyFaceUV"),
                "the lens no longer samples the sky BlackHoleSky renders.");
            Assert.IsTrue(hlsl.Contains("float2 BlackHoleSkyFaceUV("), "BlackHoleLens.hlsl lost the sky's face lookup.");
        }

        /// <summary>
        /// Each sky face is a real camera view (Unity's worldToCameraMatrix for a camera at the origin
        /// looking along the face's axis with its up vector) — not a mirror image — and the cube it
        /// draws the skybox on faces its centre. The face table itself is checked against the shader's
        /// sampling by Tools/Shaders/verify_black_hole_lens.py.
        /// </summary>
        [Test]
        public void Sky_FacesAreCameraViewsAndTheCubeFacesItsCentre()
        {
            for (int face = 0; face < BlackHoleSky.FaceCount; face++)
            {
                var rotation = Quaternion.LookRotation(BlackHoleSky.FaceForward[face], BlackHoleSky.FaceUp[face]);
                var unity = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one).inverse;
                var ours = BlackHoleSky.FaceView(face);
                for (int i = 0; i < 16; i++)
                    Assert.AreEqual(unity[i], ours[i], 1e-5f, $"sky face {face}'s view is not Unity's camera view along its axis (element {i}).");
                Assert.AreEqual(1f, Vector3.Dot(Vector3.Cross(BlackHoleSky.FaceUp[face], BlackHoleSky.FaceForward[face]),
                    Vector3.Cross(BlackHoleSky.FaceUp[face], BlackHoleSky.FaceForward[face])), 1e-5f, $"sky face {face}'s basis is not orthonormal.");
            }

            var cube = BlackHoleSky.Cube();
            var vertices = cube.vertices;
            var triangles = cube.triangles;
            Assert.AreEqual(36, triangles.Length, "the sky cube is not 12 triangles.");
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Assert.Less(Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c), 0f,
                    $"sky cube triangle {t / 3} faces outward — a back-face-culling skybox would not draw from inside it.");
            }
        }

        [Test]
        public void TestScene_ExistsAndCarriesTheHarnessWiredToItsConfig()
        {
            Assert.IsTrue(File.Exists(TestScenePath), $"{TestScenePath} is missing — run FrogletTools > Scene Setup > Setup Black Hole Test Scene.");
            string scene = File.ReadAllText(TestScenePath);
            Assert.IsTrue(scene.Contains("CosmicShore.Utility.BlackHoleTestHarness"), "BlackHoleTest.unity carries no BlackHoleTestHarness.");
            Assert.IsTrue(scene.Contains("CosmicShore.Gameplay.ThemeManager"), "BlackHoleTest.unity carries no ThemeManager — the first laid prism would NRE.");
            Assert.IsTrue(scene.Contains("PrismManagers"), "BlackHoleTest.unity carries no PrismManagers instance.");
            string hyperSea = AssetDatabase.AssetPathToGUID("Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat");
            Assert.IsFalse(string.IsNullOrEmpty(hyperSea), "HyperSeaSkybox.mat is missing.");
            Assert.IsTrue(scene.Contains($"m_SkyboxMaterial: {{fileID: 2100000, guid: {hyperSea}, type: 2}}"),
                "BlackHoleTest.unity's skybox is not the HyperSea sky — the hole would sit in Unity's default sky.");

            var testConfig = AssetDatabase.LoadAssetAtPath<BlackHoleTestConfigSO>("Assets/Resources/BlackHoleTestConfig.asset");
            Assert.IsNotNull(testConfig, "Assets/Resources/BlackHoleTestConfig.asset is missing.");
            Assert.IsNotNull(testConfig.PrismPrefab, "BlackHoleTestConfig has no prism prefab — Spawn would do nothing.");
            Assert.LessOrEqual((long)testConfig.DefaultCounts.x * testConfig.DefaultCounts.y * testConfig.DefaultCounts.z,
                testConfig.MaxTotalPrisms, "BlackHoleTestConfig's default field exceeds its own cap.");
        }
    }
}
#endif
