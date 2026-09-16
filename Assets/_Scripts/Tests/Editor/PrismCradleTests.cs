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
        // Position 0, Normal 1, Tangent 2, OutPosition 3, OutNormal 4.
        const int SlotTangent = 2;
        const int SlotOutPosition = 3;
        const int SlotOutNormal = 4;

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

            Assert.IsTrue(hlsl.Contains("void PrismCradleDeform_float(float3 Position, float3 Normal, float3 Tangent,"),
                "PrismCradle.hlsl no longer declares PrismCradleDeform_float(Position, Normal, Tangent, ...) — every " +
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

                // LAST on the chain: the cradle feeds the blocks DIRECTLY. A node spliced after it
                // would operate on a per-face rigid motion it knows nothing about.
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

                // The wedge id: Tangent is fed by an OBJECT-space Tangent Vector node. A world-space
                // one names the wrong wedge on every rotated prism; a missing one collapses the
                // cradle to whole faces about an off-centre pivot, silently.
                var tanSrc = edges.FirstOrDefault(e => e.inNode == cradleId && e.inSlot == SlotTangent);
                Assert.IsNotNull(tanSrc.outNode, $"{graphPath}: {FunctionName}.Tangent is unconnected — no vertex can name its wedge.");
                var tanBlock = blocks.FirstOrDefault(b => b.Contains($"\"m_ObjectId\": \"{tanSrc.outNode}\""));
                Assert.IsNotNull(tanBlock, $"{graphPath}: the node feeding {FunctionName}.Tangent is missing from the file.");
                Assert.IsTrue(tanBlock.Contains("\"m_Type\": \"UnityEditor.ShaderGraph.TangentVectorNode\""),
                    $"{graphPath}: {FunctionName}.Tangent must be fed by a Tangent Vector node.");
                Assert.IsTrue(Regex.IsMatch(tanBlock, "\"m_Space\":\\s*0\\b"),
                    $"{graphPath}: the cradle's Tangent Vector node is not OBJECT space (m_Space 0).");

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
                $"PrismCradleConfig is not sane (outer {config.OuterRange}, inner {config.InnerRange}): the shader treats that band as OFF.");
            Assert.Greater(config.OuterRange, config.InnerRange,
                "The cradle band must ramp: outer strictly wider than inner, or the smoothstep divides by zero.");
            Assert.Greater(config.NeighbourSpread, 0f,
                "NeighbourSpread must be positive: at 0 the adjacency smoothstep divides by zero and no neighbour ever hands off.");
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
