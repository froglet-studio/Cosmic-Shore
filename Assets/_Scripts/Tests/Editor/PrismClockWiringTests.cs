#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using CosmicShore.Editor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// CI gate for PrismClockWiringValidator.Specs: the five foundational clock
    /// clusters (grow, color, explosion, suction, flight) plus shield-morph and
    /// jiggle Hybrid-Per-Instance properties, the live non-clock families Specs
    /// now names (erosion / back-face / destruction sight), their unexposed
    /// globals (NOT Hybrid Per Instance), and the load-bearing CF edges.
    /// PrismOcclusionFade is deliberately NOT on Specs — Validate() delegates
    /// that census to PrismOcclusionWiringValidator.CheckGraphWiring so the
    /// clock menu cannot print ALL PRESENT while the corridor is unwired.
    ///
    /// The menu item that owns Specs has zero callers in Assets/, so a
    /// ShaderGraph revert, a bad merge, or a reimport that drops a property
    /// used to merge green. This suite iterates the same Specs — it does not
    /// keep its own property list.
    ///
    /// Pattern copied from <c>PrismShieldMorphTests</c>: parse the graph JSON
    /// as blank-line-delimited blocks after CRLF normalisation.
    ///
    /// All assertions run from assets alone — no play mode.
    /// </summary>
    public class PrismClockWiringTests
    {
        static readonly string[] FoundationalFamilies =
        {
            "PrismGrowScale",
            "PrismColorLerp",
            "PrismExplosionClock",
            "PrismSuctionClock",
            "PrismFlightClock",
        };

        static readonly string[] ShieldMorphProps =
        {
            "_ShieldMorphStartTime", "_ShieldMorphDuration",
            "_ShieldMorphDirection", "_ShieldMorphOffset",
        };

        [Test]
        public void SpecsNameTheFiveFoundationalClockFamilies()
        {
            var named = new HashSet<string>(PrismClockWiringValidator.Specs
                .SelectMany(s => s.CustomFunctions ?? System.Array.Empty<string>()));
            foreach (var fn in FoundationalFamilies)
            {
                Assert.IsTrue(named.Contains(fn),
                    $"PrismClockWiringValidator.Specs no longer name {fn} — emptying CustomFunctions " +
                    "must not silently pass this suite. The five foundational clusters stay on the contract.");
            }
        }

        [Test]
        public void OcclusionFade_IsNotOnClockSpecs_CorridorIsDelegated()
        {
            foreach (var spec in PrismClockWiringValidator.Specs)
            {
                Assert.IsFalse(
                    (spec.CustomFunctions ?? System.Array.Empty<string>()).Contains("PrismOcclusionFade"),
                    $"{spec.GraphName}: PrismOcclusionFade must not live on clock Specs — " +
                    "Validate() delegates that census to PrismOcclusionWiringValidator.CheckGraphWiring " +
                    "so one SoT owns the corridor.");
                Assert.IsFalse(
                    (spec.RequiredProps ?? System.Array.Empty<string>()).Contains("_PrismOcclusionTarget"),
                    $"{spec.GraphName}: corridor globals must not live on RequiredProps " +
                    "(those assert Hybrid Per Instance; corridor uniforms are unexposed globals).");
            }
        }

        [Test]
        public void Erosion_IsExplodingOnly_SightAndBackFace_AreOnBothLiveGraphs()
        {
            var byName = PrismClockWiringValidator.Specs.ToDictionary(s => s.GraphName);
            Assert.IsTrue(byName.ContainsKey("BlockGraph") && byName.ContainsKey("ExplodingBlockGraph")
                          && byName.ContainsKey("SuctionGraph"));

            Assert.IsFalse(byName["BlockGraph"].CustomFunctions.Contains("PrismErosionFade"),
                "PrismErosionFade is the exploding-debris UV0 wipe — it does not belong on BlockGraph.");
            Assert.IsTrue(byName["ExplodingBlockGraph"].CustomFunctions.Contains("PrismErosionFade"),
                "ExplodingBlockGraph must name PrismErosionFade — a revert silently returns debris fade " +
                "to the view-anchored corridor dither.");
            Assert.IsFalse(byName["SuctionGraph"].CustomFunctions.Contains("PrismErosionFade"));

            foreach (var live in new[] { "BlockGraph", "ExplodingBlockGraph" })
            {
                Assert.IsTrue(byName[live].CustomFunctions.Contains("PrismBackFaceFade"),
                    $"{live} must name PrismBackFaceFade.");
                Assert.IsTrue(byName[live].CustomFunctions.Contains("PrismDestructionSight"),
                    $"{live} must name PrismDestructionSight.");
                CollectionAssert.AreEquivalent(
                    PrismClockWiringValidator.DestructionSightGlobals,
                    byName[live].UnexposedGlobals,
                    $"{live}: UnexposedGlobals must be the five sight uniforms (not Hybrid Per Instance).");
            }

            Assert.IsFalse(byName["SuctionGraph"].CustomFunctions.Contains("PrismBackFaceFade"));
            Assert.IsFalse(byName["SuctionGraph"].CustomFunctions.Contains("PrismDestructionSight"));
            Assert.IsEmpty(byName["SuctionGraph"].UnexposedGlobals);
            Assert.IsEmpty(byName["SuctionGraph"].EdgeChecks);

            foreach (var live in new[] { "BlockGraph", "ExplodingBlockGraph" })
            {
                Assert.IsTrue(byName[live].CustomFunctions.Contains("PrismSuctionClock"),
                    $"{live} must name PrismSuctionClock — cell-swap suction is a live-prism stamp.");
                Assert.IsTrue(byName[live].CustomFunctions.Contains("PrismSuctionConverge"),
                    $"{live} must name PrismSuctionConverge — State without vertex lerp is a silent no-op.");
            }
            Assert.IsTrue(byName["SuctionGraph"].CustomFunctions.Contains("PrismSuctionClock"));
            Assert.IsFalse(byName["SuctionGraph"].CustomFunctions.Contains("PrismSuctionConverge"),
                "SuctionGraph keeps SequentialFaceConverger — do not splice PrismSuctionConverge there.");
        }

        [Test]
        public void AutoWireSource_StampsShieldMorphOnBothLiveGraphs()
        {
            // Auto-Wire → Validate closed by adding _ShieldMorph* to the C# wirer
            // (the CF + edges stay python-owned). Jobs is private; the source is
            // the contract the menu item runs.
            string path = "Assets/_Scripts/Editor/PrismClockGraphWirer.cs";
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            string text = File.ReadAllText(path);
            foreach (var prop in ShieldMorphProps)
            {
                Assert.IsTrue(text.Contains($"F(\"{prop}\""),
                    $"PrismClockGraphWirer no longer stamps {prop} — Auto-Wire → Validate Clock Wiring " +
                    "silently diverges again (the shield morph was wired by python, not by this tool).");
            }
            Assert.IsTrue(text.Contains("stay python-owned") || text.Contains("python-owned"),
                "PrismClockGraphWirer must declare which families it delegates to Tools/Shaders — " +
                "silently diverging is the thing Prompt 8 closed.");
        }

        static readonly string[] LiveSuctionProps =
        {
            "_SuctionStartTime", "_SuctionDuration",
            "_SuctionDirection", "_SuctionGrowDelay",
        };

        [Test]
        public void AutoWireSource_StampsSuctionOnBothLiveGraphs()
        {
            string path = "Assets/_Scripts/Editor/PrismClockGraphWirer.cs";
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            string text = File.ReadAllText(path);
            foreach (var prop in LiveSuctionProps)
            {
                Assert.IsTrue(text.Contains($"F(\"{prop}\""),
                    $"PrismClockGraphWirer no longer stamps {prop} — Auto-Wire → Validate Clock Wiring " +
                    "silently diverges (live suction CFs stay python-owned).");
            }
            Assert.IsTrue(text.Contains("V3(\"_Location\""),
                "PrismClockGraphWirer must stamp _Location on live graphs — StampSuctionClock writes it.");
        }

        [Test]
        public void EveryRequiredClockProperty_IsHybridPerInstance_AndEachFamilyHasItsCustomFunction()
        {
            Assert.IsNotEmpty(PrismClockWiringValidator.Specs,
                "PrismClockWiringValidator.Specs is empty — there is no contract to gate.");

            foreach (var spec in PrismClockWiringValidator.Specs)
            {
                string path = PrismClockWiringValidator.FindGraphPath(spec.GraphName);
                Assert.IsNotNull(path,
                    $"{spec.GraphName}: .shadergraph asset not found at the known Graph/PrismGraphs paths.");
                Assert.IsTrue(File.Exists(path), $"{spec.GraphName}: {path} is missing.");

                // Normalize CRLF first: a Windows checkout otherwise collapses the whole
                // file into one block and every block-scoped check reads the wrong property.
                string text = File.ReadAllText(path).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

                Assert.IsNotNull(spec.RequiredProps,
                    $"{spec.GraphName}: Specs.RequiredProps is null.");
                foreach (var prop in spec.RequiredProps)
                {
                    bool found = PrismClockWiringValidator.TryFindPropertyBlock(blocks, prop, out var block);
                    Assert.IsTrue(found,
                        $"{spec.GraphName}: required clock property {prop} is missing — a ShaderGraph revert, " +
                        "bad merge, or reimport dropped it. Restore the property block or re-run " +
                        "FrogletTools > Ecology > Prism Animation > Validate Clock Wiring.");
                    Assert.IsTrue(block.Contains(PrismClockWiringValidator.HybridPerInstanceOverride),
                        $"{spec.GraphName}: {prop} is not Hybrid Per Instance " +
                        "(hlslDeclarationOverride: 3) — per-prism stamps cannot reach the shader " +
                        "and the visual snaps.");
                }

                foreach (var prop in spec.UnexposedGlobals ?? System.Array.Empty<string>())
                {
                    Assert.IsFalse(spec.RequiredProps.Contains(prop),
                        $"{spec.GraphName}: unexposed global {prop} must not live in RequiredProps — " +
                        "that list asserts Hybrid Per Instance, and these are ONE value for the whole frame.");
                    bool found = PrismClockWiringValidator.TryFindPropertyBlock(blocks, prop, out var block);
                    Assert.IsTrue(found,
                        $"{spec.GraphName}: unexposed global {prop} is missing.");
                    Assert.IsFalse(block.Contains("\"m_GeneratePropertyBlock\": true"),
                        $"{spec.GraphName}: {prop} is EXPOSED — Shader.SetGlobal* cannot drive it.");
                    Assert.IsFalse(block.Contains(PrismClockWiringValidator.HybridPerInstanceOverride),
                        $"{spec.GraphName}: {prop} is Hybrid Per Instance — it is a frame global, not per-prism data.");
                }

                Assert.IsNotNull(spec.CustomFunctions,
                    $"{spec.GraphName}: Specs.CustomFunctions is null.");
                Assert.IsNotEmpty(spec.CustomFunctions,
                    $"{spec.GraphName}: Specs.CustomFunctions is empty — a family with no node is un-gated.");
                foreach (var fn in spec.CustomFunctions)
                {
                    string hint = PrismClockWiringValidator.CustomFunctionSourceHint(fn);
                    Assert.IsTrue(text.Contains($"\"m_FunctionName\": \"{fn}\""),
                        $"{spec.GraphName}: Custom Function node {fn} is missing — the {spec.Purpose} " +
                        $"family will snap until the node is restored (Source = {hint}).");

                    string guid = PrismClockWiringValidator.ExpectedCustomFunctionSourceGuid(fn);
                    var cfBlock = blocks.FirstOrDefault(b =>
                        b.Contains("CustomFunctionNode") &&
                        b.Contains($"\"m_FunctionName\": \"{fn}\""));
                    Assert.IsNotNull(cfBlock,
                        $"{spec.GraphName}: Custom Function node {fn} has no CustomFunctionNode block.");
                    Assert.IsTrue(cfBlock.Contains($"\"m_FunctionSource\": \"{guid}\""),
                        $"{spec.GraphName}: {fn} sources the wrong HLSL (want {hint}, GUID {guid}).");
                }

                foreach (var edge in spec.EdgeChecks ?? System.Array.Empty<PrismClockWiringValidator.GraphEdgeCheck>())
                {
                    bool ok = PrismClockWiringValidator.GraphHasSpecifiedEdge(blocks, edge, out string fault);
                    Assert.IsTrue(ok,
                        $"{spec.GraphName}: {fault}. A CF node without this edge is not the splice — " +
                        "restore via Tools/Shaders/wire_*.py, not by re-adding the node alone.");
                }
            }
        }
    }
}
#endif
