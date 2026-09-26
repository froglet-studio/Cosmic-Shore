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
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the Urchin's cradle (Docs/PRISM_ANIMATION.md §4.7.2) — the third
    /// citizen of §4.7's global-uniform shape. Its failure modes are SILENT: a graph that lost
    /// the node renders every prism exactly as before and nothing logs; a bank length that
    /// drifts between the C# and the HLSL leaves the tail of the bank unread; a source that is
    /// no longer ensured on the Urchin simply never publishes. Every check runs from assets
    /// alone — no play mode — and shares its constants with the validator so the two cannot
    /// disagree about which graphs, which file and which guid.
    /// </summary>
    public class PrismCradleTests
    {
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismCradle.hlsl";
        const string FunctionName = "PrismCradleDeform";
        const string UrchinPrefabPath = "Assets/_Prefabs/Spacevessels/Urchin.prefab";

        static readonly string[] LiveGraphs =
        {
            "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
            "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
        };

        // Slot ids in the order the HLSL declares its parameters (inputs first, then outputs):
        // Position 0, Normal 1, OutPosition 2, OutNormal 3.
        const int SlotOutPosition = 2;
        const int SlotOutNormal = 3;

        static string[] Blocks(string path)
        {
            // Normalise CRLF first: a Windows checkout otherwise collapses the whole file into
            // one block and every block-scoped check reads the wrong property.
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

        [Test]
        public void CradleHlsl_ExistsDeclaresTheFunctionAndTheFileScopeBank()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing.");
            string hlsl = File.ReadAllText(HlslPath);

            Assert.IsTrue(hlsl.Contains("void PrismCradleDeform_float(float3 Position, float3 Normal,"),
                "PrismCradle.hlsl no longer declares PrismCradleDeform_float(Position, Normal, ...) — every " +
                "wired graph would fail to compile, or bind its slots to the wrong parameters.");

            // The bank is FILE-SCOPE arrays (Shader Graph has no array property type, and an
            // array inside UnityPerMaterial breaks SRP batching) — so they must be declared here,
            // outside any CBUFFER, at the length the C# publisher writes.
            foreach (var decl in new[]
                     {
                         "float4 _PrismCradleCentre[PRISM_CRADLE_SLOTS]",
                         "float4 _PrismCradleWeight[PRISM_CRADLE_SLOTS]",
                         "float4 _PrismCradleParams",
                     })
            {
                Assert.IsTrue(hlsl.Contains(decl), $"PrismCradle.hlsl no longer declares `{decl}`.");
            }
            Assert.IsFalse(hlsl.Contains("CBUFFER_START"),
                "PrismCradle.hlsl declares a CBUFFER — the bank must stay a file-scope global or SRP batching breaks.");

            var slots = Regex.Match(hlsl, @"#define PRISM_CRADLE_SLOTS (\d+)");
            Assert.IsTrue(slots.Success, "PRISM_CRADLE_SLOTS is not #defined in PrismCradle.hlsl.");
            Assert.AreEqual(PrismCradle.Slots, int.Parse(slots.Groups[1].Value),
                "PrismCradle.Slots (C#) and PRISM_CRADLE_SLOTS (HLSL) disagree — the publisher would write " +
                "a bank the shader reads at a different length. Change both together.");

            // The guid the wirer and the validator pin must be the one the .meta actually carries.
            string meta = File.ReadAllText(HlslPath + ".meta");
            var guid = Regex.Match(meta, @"guid:\s*([0-9a-f]{32})");
            Assert.IsTrue(guid.Success, "PrismCradle.hlsl.meta carries no guid.");
            Assert.AreEqual(PrismClockWiringValidator.CradleHlslGuid, guid.Groups[1].Value,
                "PrismClockWiringValidator.CradleHlslGuid does not match PrismCradle.hlsl.meta.");
        }

        [Test]
        public void EveryLiveGraph_CarriesTheCradleLastOnBothVertexBlocks()
        {
            foreach (var graphPath in LiveGraphs)
            {
                Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
                var blocks = Blocks(graphPath);

                Assert.IsTrue(PrismClockWiringValidator.TryFindCustomFunctionNodeId(blocks, FunctionName, out var cradleId),
                    $"{graphPath} has no {FunctionName} Custom Function node — run Tools/Shaders/wire_prism_cradle.py.");

                var cradleBlock = blocks.First(b => b.Contains($"\"m_FunctionName\": \"{FunctionName}\""));
                Assert.IsTrue(cradleBlock.Contains($"\"m_FunctionSource\": \"{PrismClockWiringValidator.CradleHlslGuid}\""),
                    $"{graphPath}: {FunctionName} does not source PrismCradle.hlsl.");

                var edges = PrismClockWiringValidator.ParseEdges(blocks[0]);
                string posBlock = BlockObjectId(blocks, "VertexDescription.Position");
                string nrmBlock = BlockObjectId(blocks, "VertexDescription.Normal");

                var posFeeders = edges.Where(e => e.inNode == posBlock).ToList();
                var nrmFeeders = edges.Where(e => e.inNode == nrmBlock).ToList();
                Assert.AreEqual(1, posFeeders.Count, $"{graphPath}: VertexDescription.Position must have exactly one feeder.");
                Assert.AreEqual(1, nrmFeeders.Count, $"{graphPath}: VertexDescription.Normal must have exactly one feeder.");

                // LAST on the chain: the cradle feeds the blocks DIRECTLY. A node spliced after
                // it would operate on a draped position and normal it knows nothing about.
                Assert.AreEqual((cradleId, SlotOutPosition), (posFeeders[0].outNode, posFeeders[0].outSlot),
                    $"{graphPath}: VertexDescription.Position is not fed by {FunctionName}.OutPosition — the cradle must be LAST.");
                Assert.AreEqual((cradleId, SlotOutNormal), (nrmFeeders[0].outNode, nrmFeeders[0].outSlot),
                    $"{graphPath}: VertexDescription.Normal is not fed by {FunctionName}.OutNormal — the cradle must be LAST.");

                // ...and it wraps the chains rather than replacing them: both inputs are fed, and
                // not by itself.
                foreach (var (slot, label) in new[] { (0, "Position"), (1, "Normal") })
                {
                    var src = edges.FirstOrDefault(e => e.inNode == cradleId && e.inSlot == slot);
                    Assert.IsNotNull(src.outNode, $"{graphPath}: {FunctionName}.{label} is unconnected — the vertex chain was dropped.");
                    Assert.AreNotEqual(cradleId, src.outNode, $"{graphPath}: {FunctionName}.{label} is fed by itself.");
                }

                // The bank must NOT also exist as graph properties: a same-named property would
                // shadow the file-scope declaration and read the per-material default (zero).
                foreach (var name in new[] { "_PrismCradleCentre", "_PrismCradleWeight", "_PrismCradleParams" })
                {
                    Assert.IsFalse(blocks.Any(b => b.Contains("ShaderProperty") &&
                                                   (b.Contains($"\"m_DefaultReferenceName\": \"{name}\"") ||
                                                    b.Contains($"\"m_OverrideReferenceName\": \"{name}\""))),
                        $"{graphPath} declares {name} as a graph PROPERTY — the cradle bank is a file-scope global in the HLSL, never a property.");
                }
            }
        }

        [Test]
        public void Specs_NameTheCradleOnBothLiveGraphs_AndNotOnSuction()
        {
            var byName = PrismClockWiringValidator.Specs.ToDictionary(s => s.GraphName);
            foreach (var graph in new[] { "BlockGraph", "ExplodingBlockGraph" })
            {
                Assert.IsTrue(byName[graph].CustomFunctions.Contains(FunctionName),
                    $"PrismClockWiringValidator.Specs[{graph}] no longer names {FunctionName} — the menu validator would print ALL PRESENT with the cradle unwired.");
            }
            // Consumed mass is not mass the hull rests on — the same exclusion every vertex-stage
            // family on the live graphs makes.
            Assert.IsFalse(byName["SuctionGraph"].CustomFunctions.Contains(FunctionName),
                "SuctionGraph must not carry the cradle: it renders mass being consumed.");
            Assert.AreEqual("PrismCradle.hlsl", PrismClockWiringValidator.CustomFunctionSourceHint(FunctionName));
            Assert.AreEqual(PrismClockWiringValidator.CradleHlslGuid,
                PrismClockWiringValidator.ExpectedCustomFunctionSourceGuid(FunctionName));
        }

        [Test]
        public void Config_IsSaneWhenAuthored()
        {
            var config = AssetDatabase.LoadAssetAtPath<PrismCradleConfigSO>(
                "Assets/Resources/" + PrismCradle.ConfigResourcePath + ".asset");
            if (config == null)
            {
                // No asset is a legal state (the SO's defaults apply) — but the defaults must
                // themselves be sane, or the feature silently never engages.
                config = ScriptableObject.CreateInstance<PrismCradleConfigSO>();
            }
            Assert.IsTrue(config.IsSane,
                $"PrismCradleConfig is not sane (reach {config.DrapeReach}, exponent {config.DrapeExponent}): the shader treats that as OFF.");
            Assert.Greater(config.DrapeReach, 0f,
                "DrapeReach must be positive: at 0 the falloff divides by zero and nothing outside the hull ever rises.");
            Assert.GreaterOrEqual(config.DrapeExponent, 1f,
                "DrapeExponent below 1 puts a crease at exactly the distance the drape is supposed to vanish without one.");
            // The ceiling the eased strength runs to. Above 1 the map overshoots the hull's
            // surface rather than landing on it; at 0 the cradle is off, which is what
            // `enabled` is for.
            Assert.Greater(config.MaxStrength, 0f,
                "PrismCradleConfig.MaxStrength is 0 — the cradle publishes a zero weight and nothing deforms. Use `enabled` to switch it off.");
            Assert.LessOrEqual(config.MaxStrength, 1f,
                "PrismCradleConfig.MaxStrength is above 1 — the map would push mass past the hull rather than onto it.");
        }

        [Test]
        public void Config_ResidencySwapIsInvisible()
        {
            var config = AssetDatabase.LoadAssetAtPath<PrismCradleConfigSO>(
                "Assets/Resources/" + PrismCradle.ConfigResourcePath + ".asset")
                ?? ScriptableObject.CreateInstance<PrismCradleConfigSO>();

            // The whole point of the margin: a prism gains or loses its high-poly geometry only
            // where the drape provably cannot have moved any of its vertices. A zero margin puts
            // the swap exactly ON the boundary, where a float comparison decides whether the
            // player sees a prism change shape.
            Assert.Greater(config.ResidencyMargin, 0f,
                "PrismCradleConfig.ResidencyMargin is 0 — the mesh swap would happen at exactly the distance " +
                "the drape reaches, so it is a coin toss whether the geometry change is visible.");

            // The budget. 0 means the drape only ever runs on the authored 24-triangle prism,
            // which is the look two playtests rejected; an unbounded one is the cost nobody
            // signed up for.
            Assert.Greater(config.MaxResidentPrisms, 0,
                "PrismCradleConfig.MaxResidentPrisms is 0 — nothing is ever swapped, so the drape runs on the " +
                "authored 24-triangle prism and reads as facets hinging (the look this redesign replaced).");
            Assert.LessOrEqual(config.MaxResidentPrisms, 64,
                "PrismCradleConfig.MaxResidentPrisms is above 64 — this is the feature's entire performance " +
                "budget and 'a handful of prisms' is what makes the high-poly swap affordable at all.");

            long tris = (long)config.MaxResidentPrisms * config.Subdivision * config.Subdivision * 2 * 6;
            Assert.Less(tris, 200000,
                $"The residency budget is {tris} triangles ({config.MaxResidentPrisms} prisms x subdivision " +
                $"{config.Subdivision}) — lower MaxResidentPrisms or Subdivision.");
        }

        [Test]
        public void HighPolyPrismMesh_IsTheSameSolidAtHigherDensity()
        {
            var mesh = HighPolyPrismMesh.Get(8);
            Assert.IsNotNull(mesh, "HighPolyPrismMesh.Get returned null.");

            // Same solid: a unit cube of the half-extent BOTH shipped prism mesh families use
            // (the authored Prism.asset and the built-in Cube). A different extent is a prism
            // that visibly changes size the instant it becomes resident.
            var b = mesh.bounds;
            Assert.AreEqual(HighPolyPrismMesh.HalfExtent, b.extents.x, 1e-5f, "high-poly prism x extent drifted");
            Assert.AreEqual(HighPolyPrismMesh.HalfExtent, b.extents.y, 1e-5f, "high-poly prism y extent drifted");
            Assert.AreEqual(HighPolyPrismMesh.HalfExtent, b.extents.z, 1e-5f, "high-poly prism z extent drifted");
            Assert.AreEqual(Vector3.zero, b.center, "high-poly prism is not centred on its origin");

            Assert.AreEqual(8 * 8 * 2 * 6, mesh.triangles.Length / 3, "high-poly prism triangle count");
            Assert.AreEqual(9 * 9 * 6, mesh.vertexCount, "high-poly prism vertex count (per-face grids, hard edges)");

            // HARD edges: every normal is one of the six axis directions, and every vertex's
            // normal agrees with the face it sits on. A smoothed prism reads as a ball.
            var verts = mesh.vertices;
            var norms = mesh.normals;
            Assert.AreEqual(verts.Length, norms.Length, "high-poly prism has no per-vertex normals");
            for (int i = 0; i < norms.Length; i++)
            {
                float a = Mathf.Abs(norms[i].x) + Mathf.Abs(norms[i].y) + Mathf.Abs(norms[i].z);
                Assert.AreEqual(1f, a, 1e-4f, $"vertex {i}'s normal is not an axis direction — the faces are smoothed");
                Assert.AreEqual(HighPolyPrismMesh.HalfExtent, Vector3.Dot(verts[i], norms[i]), 1e-4f,
                    $"vertex {i} does not lie on the face its normal names");
            }

            // OUTWARD winding: every triangle's geometric normal agrees with its vertices'.
            var tris = mesh.triangles;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 g = Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]);
                Assert.Greater(Vector3.Dot(g.normalized, norms[tris[t]]), 0.99f,
                    $"triangle {t / 3} is wound INWARD — the prism would render inside-out the instant it became resident");
            }

            // Shared and cached: the whole reason a swapped prism still batches.
            Assert.AreSame(mesh, HighPolyPrismMesh.Get(8), "HighPolyPrismMesh.Get is not returning a cached shared mesh");
            Assert.IsTrue(HighPolyPrismMesh.IsHighPoly(mesh), "HighPolyPrismMesh does not recognise its own mesh");
            Assert.IsFalse(HighPolyPrismMesh.IsHighPoly(null), "HighPolyPrismMesh claims null is one of its meshes");
        }

        [Test]
        public void UrchinPrefab_CarriesTheRidingTransformerThatEnsuresTheSource()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UrchinPrefabPath);
            Assert.IsNotNull(prefab, $"{UrchinPrefabPath} is missing.");
            // The source is ENSURED by GunVesselTransformer.Initialize rather than authored, so
            // the prefab-level fact to hold is that the Urchin still flies on that transformer.
            Assert.IsNotNull(prefab.GetComponent<GunVesselTransformer>(),
                "Urchin.prefab no longer carries GunVesselTransformer — nothing would ensure PrismCradleSource on it.");
            // And if someone DID author one, it must be the one and only.
            Assert.LessOrEqual(prefab.GetComponents<PrismCradleSource>().Length, 1,
                "Urchin.prefab carries more than one PrismCradleSource.");
        }
    }
}
#endif
