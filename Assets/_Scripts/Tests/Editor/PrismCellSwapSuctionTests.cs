#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Structural gates for Docs/PRISM_ANIMATION.md §5 C9 — Cell.RequestCellSwap
    /// retiring-world suction on the GPU clock. A stamp that lands on a prototype
    /// without the suction cluster, or a graph without PrismSuctionConverge, is a
    /// silent no-op (the trap that scoped this prompt). These tests fail that class
    /// of regression without needing play mode.
    /// </summary>
    public class PrismCellSwapSuctionTests
    {
        const string WiringTool = "Tools/Shaders/wire_prism_suction_clock.py";
        const string FunctionClock = "PrismSuctionClock";
        const string FunctionConverge = "PrismSuctionConverge";
        const string RenderServicePath = "Assets/_Scripts/Controller/ECS/Rendering/PrismRenderService.cs";
        const string PrismPath = "Assets/_Scripts/Controller/Vessel/Prism.cs";
        const string CellPath = "Assets/_Scripts/Controller/Environment/Cell.cs";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl";

        static readonly string[] LiveGraphs =
        {
            "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
            "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
        };

        static readonly string[] PerInstanceProps =
        {
            "_SuctionStartTime", "_SuctionDuration", "_SuctionDirection",
            "_SuctionGrowDelay", "_Location",
        };

        [Test]
        public void LiveGraphs_DeclareSuctionPropertiesAndConverge()
        {
            foreach (var graphPath in LiveGraphs)
            {
                Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
                string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var prop in PerInstanceProps)
                {
                    var block = blocks.FirstOrDefault(b =>
                        (b.Contains($"\"m_DefaultReferenceName\": \"{prop}\"") ||
                         b.Contains($"\"m_OverrideReferenceName\": \"{prop}\"")) &&
                        b.Contains("ShaderProperty"));
                    Assert.IsNotNull(block,
                        $"{graphPath} does not declare {prop} — run {WiringTool}.");
                    Assert.IsTrue(block.Contains("\"m_GeneratePropertyBlock\": true"),
                        $"{graphPath}: {prop} is UNEXPOSED — a per-instance stamp cannot reach it.");
                    Assert.IsTrue(block.Contains("\"hlslDeclarationOverride\": 3"),
                        $"{graphPath}: {prop} is not Hybrid Per Instance — every prism would render the " +
                        "material default and the stamp would go nowhere.");
                }

                Assert.IsTrue(text.Contains($"\"m_FunctionName\": \"{FunctionClock}\""),
                    $"{graphPath} has no {FunctionClock} Custom Function node. Run {WiringTool}.");
                Assert.IsTrue(text.Contains($"\"m_FunctionName\": \"{FunctionConverge}\""),
                    $"{graphPath} has no {FunctionConverge} Custom Function node — State without a " +
                    $"vertex lerp is a silent no-op. Run {WiringTool}.");
            }
        }

        [Test]
        public void SuctionGraph_KeepsClockAndDoesNotCarryConverge()
        {
            const string path = "Assets/_Graphics/Materials/Graphs/SuctionGraph.shadergraph";
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            string text = File.ReadAllText(path);
            Assert.IsTrue(text.Contains($"\"m_FunctionName\": \"{FunctionClock}\""),
                "SuctionGraph lost PrismSuctionClock — fauna consumption VFX would snap.");
            Assert.IsFalse(text.Contains($"\"m_FunctionName\": \"{FunctionConverge}\""),
                "SuctionGraph must NOT carry PrismSuctionConverge — SequentialFaceConverger owns " +
                "per-face consumption. Live-prism whole-prism lerp is BlockGraph/ExplodingBlockGraph.");
        }

        static readonly string[] ComponentTypeNames =
        {
            "PrismSuctionStartTimeOverride", "PrismSuctionDurationOverride",
            "PrismSuctionDirectionOverride", "PrismSuctionGrowDelayOverride",
            "PrismImplosionLocationOverride",
        };

        [Test]
        public void GraphProperties_AndEntityComponents_MatchOneForOne()
        {
            var assembly = typeof(CosmicShore.ECS.PrismRenderService).Assembly;
            foreach (var typeName in ComponentTypeNames)
            {
                var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                Assert.IsNotNull(type, $"{typeName} does not exist — StampSuctionClock cannot compile.");
                Assert.IsTrue(typeof(Unity.Entities.IComponentData).IsAssignableFrom(type),
                    $"{typeName} is not an IComponentData — it can never reach the GPU.");
                Assert.IsTrue(type.GetCustomAttributes(false)
                        .Any(a => a.GetType().Name == "MaterialProperty"),
                    $"{typeName} has lost its [MaterialProperty] attribute — Entities Graphics will " +
                    "not upload it, so the stamp writes into nothing and the world snaps at drain.");
            }

            const string propsPath = "Assets/_Scripts/Controller/ECS/Rendering/PrismRenderProperties.cs";
            Assert.IsTrue(File.Exists(propsPath), $"{propsPath} is missing.");
            string propsSrc = File.ReadAllText(propsPath);
            var declared = new HashSet<string>();
            foreach (Match m in Regex.Matches(propsSrc, @"\[MaterialProperty\(""(_\w+)""\)\]"))
            {
                string name = m.Groups[1].Value;
                if (name.StartsWith("_Suction", StringComparison.Ordinal) || name == "_Location")
                    declared.Add(name);
            }

            CollectionAssert.AreEquivalent(PerInstanceProps, declared.ToArray(),
                "The suction cluster's CPU and GPU halves have drifted: the [MaterialProperty] " +
                $"reference names in {propsPath} do not match the Hybrid-Per-Instance properties " +
                "on the live prism graphs.");

            Assert.IsTrue(File.Exists(RenderServicePath), $"{RenderServicePath} is missing.");
            string service = File.ReadAllText(RenderServicePath);
            foreach (var t in ComponentTypeNames)
            {
                Assert.IsTrue(service.Contains($"em.AddComponentData(prototype, new {t}"),
                    $"{t} is not added on a prototype in PrismRenderService.GetPrototype — " +
                    "StampSuctionClock's HasComponent probe will fail for every live prism, forever.");
            }

            int locationAdds = Regex.Matches(service,
                @"em\.AddComponentData\(prototype, new PrismImplosionLocationOverride").Count;
            Assert.GreaterOrEqual(locationAdds, 2,
                "PrismImplosionLocationOverride must be added on BOTH the Implosion set AND the " +
                "Prism set — C9's live-prism stamp writes Location, and AddComponentData on a live " +
                "entity is a per-prism archetype move.");
        }

        [Test]
        public void ClearPrismStamps_AlsoClearsTheSuction()
        {
            string service = File.ReadAllText(RenderServicePath);
            int idx = service.IndexOf("public static void ClearPrismStamps", StringComparison.Ordinal);
            Assert.Greater(idx, 0, "ClearPrismStamps not found in PrismRenderService.");
            string body = service.Substring(idx, Math.Min(800, service.Length - idx));
            Assert.IsTrue(body.Contains("ClearSuctionClockStamp"),
                "ClearPrismStamps does not clear the suction clock — a pooled prism can inherit the " +
                "previous life's convergence and fly toward a stale cell centre on next pull.");
        }

        [Test]
        public void StampSuctionClock_GatesOnLocationBeforeAnyWrite()
        {
            string service = File.ReadAllText(RenderServicePath);
            int idx = service.IndexOf("public static bool StampSuctionClock", StringComparison.Ordinal);
            Assert.Greater(idx, 0, "StampSuctionClock not found.");
            int end = service.IndexOf("public static", idx + 1, StringComparison.Ordinal);
            string body = service.Substring(idx, (end > idx ? end : service.Length) - idx);
            int hasLoc = body.IndexOf("HasComponent<PrismImplosionLocationOverride>", StringComparison.Ordinal);
            int setLoc = body.IndexOf("SetComponentData(handle.Entity, new PrismImplosionLocationOverride",
                StringComparison.Ordinal);
            Assert.Greater(hasLoc, 0,
                "StampSuctionClock must HasComponent the Location override before writing — " +
                "otherwise a live prism missing it throws on SetComponentData.");
            Assert.Greater(setLoc, hasLoc,
                "Location HasComponent must run BEFORE the Location SetComponentData.");
        }

        [Test]
        public void CellSwap_StampsLivePrismsAndClearsOnPoolReturn()
        {
            Assert.IsTrue(File.Exists(CellPath), $"{CellPath} is missing.");
            string cell = File.ReadAllText(CellPath);
            Assert.IsTrue(cell.Contains("StampSuctionToward"),
                "Cell.RequestCellSwap no longer stamps live prisms — the retiring world would snap " +
                "at drain (Docs/PRISM_ANIMATION.md §3.8 #1).");
            Assert.IsTrue(cell.Contains("ClearSuctionClockStamp"),
                "Cell no longer clears the suction stamp on pooled returns — the next pull inherits " +
                "the retired world's convergence.");
            Assert.IsFalse(cell.Contains("HideForTransport"),
                "Cell.RequestCellSwap must not HideForTransport — that is C8's off-screen conveyor " +
                "gate. Cell-swap suction is a VISIBLE retirement.");
            Assert.IsFalse(cell.Contains("BeginBulkTransport"),
                "Cell.RequestCellSwap must not BeginBulkTransport — stamps are SetComponentData, " +
                "not transform writes.");
        }

        [Test]
        public void PrismStamp_ExpandsBoundsTowardTheConvergencePoint()
        {
            Assert.IsTrue(File.Exists(PrismPath), $"{PrismPath} is missing.");
            string prism = File.ReadAllText(PrismPath);
            int idx = prism.IndexOf("public void StampSuctionToward", StringComparison.Ordinal);
            Assert.Greater(idx, 0, "StampSuctionToward not found on Prism.");
            int end = prism.IndexOf("public void ClearSuctionClockStamp", idx, StringComparison.Ordinal);
            string body = prism.Substring(idx, (end > idx ? end : prism.Length) - idx);
            Assert.IsTrue(body.Contains("ResetBoundsToMesh"),
                "StampSuctionToward must ResetBoundsToMesh before encapsulating — otherwise the " +
                "culling envelope is leftover from a previous clock.");
            Assert.IsTrue(body.Contains("EncapsulateBoundsPoint"),
                "StampSuctionToward must EncapsulateBoundsPoint(objectPoint(cellCentre)) — a prism " +
                "whose bounds stay at the box frustum-culls away mid-suction (same class as explosions).");
        }

        [Test]
        public void Hlsl_DeclaresConvergeAndDurationZeroIdentity()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing.");
            string hlsl = File.ReadAllText(HlslPath);
            Assert.IsTrue(hlsl.Contains("void PrismSuctionConverge_float("),
                "PrismSuctionConverge_float is missing — live graphs would stamp State nobody reads.");
            Assert.IsTrue(hlsl.Contains("void PrismSuctionClock_float("),
                "PrismSuctionClock_float is missing.");

            int clock = hlsl.IndexOf("void PrismSuctionClock_float(", StringComparison.Ordinal);
            int converge = hlsl.IndexOf("void PrismSuctionConverge_float(", StringComparison.Ordinal);
            Assert.Greater(converge, clock,
                "PrismSuctionConverge_float should sit next to PrismSuctionClock_float.");
            string clockBody = hlsl.Substring(clock, converge - clock);
            Assert.IsTrue(clockBody.Contains("Duration <= 0.0") || clockBody.Contains("Duration <= 0"),
                "Duration <= 0 must return LegacyState — unstamped live prisms (LegacyState default 0) " +
                "stay at rest. A missing identity makes every prism in the arena converge.");
        }
    }
}
#endif

