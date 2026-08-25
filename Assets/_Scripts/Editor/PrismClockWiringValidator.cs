using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click audit of the clock-material wiring (Docs/PRISM_ANIMATION.md §4.4,
    /// STRICT MODE — no legacy fallback): checks each prism ShaderGraph's source for
    /// the required clock properties + Hybrid Per Instance flags, then checks every
    /// material actually compiled against those graphs via HasProperty (the same
    /// ground truth the runtime PrismClockDiagnostics use). Also names the live
    /// non-clock families on those graphs (erosion / back-face / destruction sight)
    /// and delegates the corridor graph census so this menu cannot say ALL PRESENT
    /// while PrismOcclusionFade is missing. Run after every wiring step — the report
    /// enumerates exactly what remains.
    /// FrogletTools > Ecology > Prism Animation> Validate Clock Wiring.
    /// </summary>
    public static class PrismClockWiringValidator
    {
        /// <summary>
        /// One graph's clock-wiring contract. Public so <c>PrismClockWiringTests</c>
        /// can iterate the same list the menu item uses — duplicating the property
        /// names would let a Specs edit and a test list drift apart, which is the
        /// class of miss this gate exists to close.
        /// </summary>
        public class GraphSpec
        {
            public string GraphName;
            public string[] RequiredProps;
            public string[] OptionalProps;
            public string[] CustomFunctions;
            /// <summary>
            /// Unexposed globals that are NOT Hybrid Per Instance (same shape as
            /// <c>_PrismClock</c>). The five destruction-sight uniforms live here —
            /// they must never go in <see cref="RequiredProps"/>, which asserts HPI 3.
            /// </summary>
            public string[] UnexposedGlobals;
            /// <summary>
            /// Load-bearing Custom Function edges. Presence of a CF node is not the
            /// splice — a revert can leave the node and drop the edge.
            /// </summary>
            public GraphEdgeCheck[] EdgeChecks;
            public string Purpose;
        }

        /// <summary>One required edge on a Custom Function node.</summary>
        public class GraphEdgeCheck
        {
            public string InputFunction;
            public int InputSlot;
            /// <summary>Null = any connected source (UV, Position, Normal, a property).</summary>
            public string OutputFunction;
            /// <summary>−1 = any slot on <see cref="OutputFunction"/>.</summary>
            public int OutputSlot = -1;
            public string Description;
        }

        public const string ClockHlslGuid = "e3f9a1c27b8d4e05b6a4c9d1f0527a83";
        public const string SightHlslGuid = "c7d41a9e5b8f4e3ab216d0f97c4e8a52";

        public static readonly string[] DestructionSightGlobals =
        {
            "_PrismSightApex", "_PrismSightAxis", "_PrismSightGape",
            "_PrismSightParams", "_PrismSightStrength",
        };

        static readonly GraphEdgeCheck[] BackFaceEdges =
        {
            new GraphEdgeCheck
            {
                InputFunction = "PrismBackFaceFade", InputSlot = 2,
                OutputFunction = "PrismOcclusionFade", OutputSlot = 4,
                Description = "back-face BaseAlpha sits AFTER the corridor Alpha",
            },
            new GraphEdgeCheck
            {
                InputFunction = "PrismBackFaceFade", InputSlot = 0,
                Description = "back-face PositionWS connected",
            },
            new GraphEdgeCheck
            {
                InputFunction = "PrismBackFaceFade", InputSlot = 1,
                Description = "back-face NormalWS connected",
            },
        };

        static readonly GraphEdgeCheck[] ExplosionErosionEdges =
        {
            new GraphEdgeCheck
            {
                InputFunction = "PrismErosionFade", InputSlot = 2,
                OutputFunction = "PrismExplosionClock", OutputSlot = 8,
                Description = "erosion BaseOpacity fed by PrismExplosionClock.Opacity",
            },
            new GraphEdgeCheck
            {
                InputFunction = "PrismErosionFade", InputSlot = 0,
                Description = "erosion UV connected",
            },
            new GraphEdgeCheck
            {
                InputFunction = "PrismErosionFade", InputSlot = 1,
                Description = "erosion Velocity connected",
            },
            new GraphEdgeCheck
            {
                InputFunction = "PrismOcclusionFade", InputSlot = 3,
                OutputFunction = "PrismErosionFade", OutputSlot = 3,
                Description = "corridor BaseAlpha fed by erosion Survival",
            },
        };

        static readonly GraphEdgeCheck[] LiveSuctionEdges =
        {
            new GraphEdgeCheck
            {
                InputFunction = "PrismSuctionConverge", InputSlot = 0,
                OutputFunction = "PrismSuctionClock", OutputSlot = 6,
                Description = "live suction Converge.State fed by PrismSuctionClock.State",
            },
            new GraphEdgeCheck
            {
                InputFunction = "PrismSuctionConverge", InputSlot = 1,
                Description = "live suction Converge.WorldLocation connected",
            },
            new GraphEdgeCheck
            {
                InputFunction = "PrismSuctionConverge", InputSlot = 2,
                Description = "live suction Converge.Position connected (flight Add retargeted)",
            },
        };

        public static readonly GraphSpec[] Specs =
        {
            new GraphSpec
            {
                GraphName = "BlockGraph",
                RequiredProps = new[]
                {
                    "_GrowStartTime", "_GrowRate", "_GrowStartFrac",
                    "_ColorStartTime", "_ColorDuration",
                    "_StartBrightColor", "_StartDarkColor", "_StartSpread",
                    "_FlightStartTime", "_FlightDuration", "_FlightVelocity",
                    "_ShieldMorphStartTime", "_ShieldMorphDuration",
                    "_ShieldMorphDirection", "_ShieldMorphOffset",
                    "_JiggleStartTime", "_JiggleDuration", "_JiggleParams",
                    "_SuctionStartTime", "_SuctionDuration", "_SuctionDirection",
                    "_SuctionGrowDelay", "_Location",
                },
                OptionalProps = new string[0],
                CustomFunctions = new[]
                {
                    "PrismGrowScale", "PrismColorLerp",
                    "PrismFlightClock", "PrismFlightSqrDistance",
                    "PrismShieldMorph", "PrismJiggleClock",
                    "PrismBackFaceFade", "PrismDestructionSight",
                    "PrismSuctionClock", "PrismSuctionConverge",
                },
                UnexposedGlobals = DestructionSightGlobals,
                EdgeChecks = Concat(BackFaceEdges, LiveSuctionEdges),
                Purpose = "grow-in bloom (PrismGrowScale, vertex) + color/state transitions (PrismColorLerp, fragment) + ballistic flight (PrismFlightClock, vertex) + shield engage/shatter morph (PrismShieldMorph, vertex) + super-shield deflection jiggle (PrismJiggleClock, vertex) + cell-swap suction (PrismSuctionClock + PrismSuctionConverge, vertex) + back-face fade + destruction sight",
            },
            new GraphSpec
            {
                GraphName = "ExplodingBlockGraph",
                // Transparent LIVE prisms rest on this graph — the grow trio + color
                // five are required here too, or their spawn bloom / steal repaint
                // snaps (loudly) while opaque prisms animate.
                RequiredProps = new[]
                {
                    "_ExplodeStartTime", "_ExplodeSpeed", "_ExplodeDuration",
                    "_GrowStartTime", "_GrowRate", "_GrowStartFrac",
                    "_ColorStartTime", "_ColorDuration",
                    "_StartBrightColor", "_StartDarkColor", "_StartSpread",
                    "_FlightStartTime", "_FlightDuration", "_FlightVelocity",
                    "_ShieldMorphStartTime", "_ShieldMorphDuration",
                    "_ShieldMorphDirection", "_ShieldMorphOffset",
                    "_JiggleStartTime", "_JiggleDuration", "_JiggleParams",
                    "_SuctionStartTime", "_SuctionDuration", "_SuctionDirection",
                    "_SuctionGrowDelay", "_Location",
                },
                OptionalProps = new string[0],
                CustomFunctions = new[]
                {
                    "PrismExplosionClock", "PrismGrowScale", "PrismColorLerp",
                    "PrismFlightClock", "PrismShieldMorph", "PrismJiggleClock",
                    "PrismErosionFade", "PrismBackFaceFade", "PrismDestructionSight",
                    "PrismSuctionClock", "PrismSuctionConverge",
                },
                UnexposedGlobals = DestructionSightGlobals,
                EdgeChecks = Concat(Concat(ExplosionErosionEdges, BackFaceEdges), LiveSuctionEdges),
                Purpose = "explosion debris flight/shatter/fade (PrismExplosionClock) + transparent live prism bloom/color/flight/shield morph/deflection + cell-swap suction + UV0 erosion + back-face fade + destruction sight",
            },
            new GraphSpec
            {
                GraphName = "SuctionGraph",
                RequiredProps = new[] { "_SuctionStartTime", "_SuctionDuration", "_SuctionDirection", "_SuctionGrowDelay" },
                OptionalProps = new string[0],
                CustomFunctions = new[] { "PrismSuctionClock" },
                UnexposedGlobals = new string[0],
                EdgeChecks = new GraphEdgeCheck[0],
                Purpose = "implosion / reverse-grow suction (PrismSuctionClock). Deliberate corridor exclusion — consumption VFX, never standing mass (PrismOcclusionWiringValidator.KnownCorridorExcludedGraphs)",
            },
        };

        /// <summary>Shader Graph Hybrid Per Instance (the only declaration a per-prism stamp can reach).</summary>
        public const string HybridPerInstanceOverride = "\"hlslDeclarationOverride\": 3";

        static readonly Regex EdgeRegex = new Regex(
            "\"m_OutputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]+)\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(\\d+)\\s*\\}\\s*,\\s*\"m_InputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]+)\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(\\d+)",
            RegexOptions.Compiled);

        [MenuItem("FrogletTools/Ecology/Prism Animation/Validate Clock Wiring")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 5,
            Description = "Clock-material law gate - unwired graphs fail loud here.",
            DocPath = "Docs/PRISM_ANIMATION.md")]
        public static void Validate()
        {
            var report = new StringBuilder();
            report.AppendLine("[PrismClock] WIRING VALIDATION — Docs/PRISM_ANIMATION.md §4.4 (STRICT MODE: unwired = loud snap, no fallback)");
            bool allRequiredPass = true;

            foreach (var spec in Specs)
            {
                string path = FindGraphPath(spec.GraphName);
                if (path == null)
                {
                    report.AppendLine($"❌ {spec.GraphName}: .shadergraph asset NOT FOUND");
                    allRequiredPass = false;
                    continue;
                }

                // Normalize line endings BEFORE splitting: Windows checkouts
                // (git autocrlf) deliver CRLF, and "\n\n" never matches inside
                // "\r\n\r\n" — the whole file became ONE block and every
                // block-scoped check ran against unrelated properties.
                string text = File.ReadAllText(path).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

                report.AppendLine($"— {spec.GraphName}  ({path})  [{spec.Purpose}]");
                foreach (var prop in spec.RequiredProps)
                    allRequiredPass &= CheckProp(report, blocks, prop, required: true);
                foreach (var prop in spec.OptionalProps)
                    CheckProp(report, blocks, prop, required: false);

                // The clock feed: _PrismClock must exist as a GLOBAL (unexposed,
                // no HPI) — published per frame by PrismClock's publisher. The
                // shader Time node is deliberately NOT used (URP feeds it from a
                // different clock domain than the stamps — the pop-in bug).
                allRequiredPass &= CheckUnexposedGlobal(report, blocks, "_PrismClock", "the global clock feed");

                foreach (var prop in spec.UnexposedGlobals ?? System.Array.Empty<string>())
                    allRequiredPass &= CheckUnexposedGlobal(report, blocks, prop, "unexposed global — not Hybrid Per Instance");

                // Custom Function nodes: match the serialized function name, not a
                // bare substring — a comment or HLSL include path containing the
                // same token is not a node. Specs.CustomFunctions is the SoT the
                // edit-mode test iterates too.
                foreach (var fn in spec.CustomFunctions)
                    allRequiredPass &= CheckCustomFunction(report, blocks, text, fn);

                foreach (var edge in spec.EdgeChecks ?? System.Array.Empty<GraphEdgeCheck>())
                {
                    if (GraphHasSpecifiedEdge(blocks, edge, out string fault))
                        report.AppendLine($"   ✅ edge: {edge.Description}");
                    else
                    {
                        report.AppendLine($"   ❌ edge: {fault}");
                        allRequiredPass = false;
                    }
                }
            }

            // Corridor graph census lives on PrismOcclusionWiringValidator so there
            // is one SoT for PrismOcclusionFade + the two unexposed corridor globals.
            // Calling it here means this menu cannot print ALL PRESENT while the
            // corridor graphs are unwired. Prefab census and material opaque+clip
            // stay on Validate Occlusion Corridor.
            report.AppendLine("— Corridor graphs (delegated to PrismOcclusionWiringValidator.CheckGraphWiring so this menu is not silently partial):");
            allRequiredPass &= PrismOcclusionWiringValidator.CheckGraphWiring(report);

            // Material-level ground truth: what the compiled shaders actually declare —
            // exactly what PrismClockDiagnostics checks at runtime.
            report.AppendLine("— Materials (compiled ground truth, same check the runtime diagnostics use):");
            int matChecked = 0, matFailed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null) continue;
                // Exact-name match: EndsWith("BlockGraph") also matched
                // "ExplodingBlockGraph", checking those materials against the
                // wrong property list (the earlier 4/22 false failures).
                var spec = Specs.FirstOrDefault(s =>
                    mat.shader.name == "Shader Graphs/" + s.GraphName ||
                    mat.shader.name.EndsWith("/" + s.GraphName));
                if (spec == null) continue;

                matChecked++;
                var missing = spec.RequiredProps.Where(p => !mat.HasProperty(p)).ToList();
                if (missing.Count > 0)
                {
                    matFailed++;
                    report.AppendLine($"   ❌ {mat.name} ({mat.shader.name}): missing {string.Join(", ", missing)}");
                }
            }
            report.AppendLine(matFailed == 0
                ? $"   ✅ {matChecked} prism-graph materials checked, all declare their required clock properties"
                : $"   ❌ {matFailed}/{matChecked} prism-graph materials missing clock properties (reimport the graph after wiring; Hybrid Per Instance flag included)");
            allRequiredPass &= matFailed == 0;

            // Instanced rendering must be on — the clock path rides it (no fallback).
            var config = Resources.Load<CosmicShore.ScriptableObjects.PrismRenderConfigSO>("PrismRenderConfig");
            if (config == null)
                report.AppendLine("   ⚠ No Resources/PrismRenderConfig asset — instanced rendering defaults OFF and the clock path has nothing to stamp (WarnNoRenderEntity will fire).");
            else if (!config.UseInstancedRendering)
            {
                report.AppendLine("   ❌ PrismRenderConfig: 'Use Instanced Rendering' is OFF — the clock path REQUIRES it (strict mode has no fallback).");
                allRequiredPass = false;
            }
            else
                report.AppendLine("   ✅ PrismRenderConfig: instanced rendering ON");

            report.AppendLine(allRequiredPass
                ? "RESULT: ✅ ALL REQUIRED WIRING PRESENT — prism animation is fully GPU-clocked. Run the play-mode smoke test to confirm visually."
                : "RESULT: ❌ WIRING INCOMPLETE — every ❌ above will snap visibly and log [PrismClock] errors in play mode until fixed (§4.4 has the exact steps).");

            if (allRequiredPass) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        public static bool TryFindPropertyBlock(string[] blocks, string prop, out string block)
        {
            // A property block carries its reference name (default or override) and
            // the Hybrid Per Instance flag in one serialized object.
            block = blocks.FirstOrDefault(b =>
                (b.Contains($"\"m_DefaultReferenceName\": \"{prop}\"") ||
                 b.Contains($"\"m_OverrideReferenceName\": \"{prop}\"")) &&
                b.Contains("ShaderProperty"));
            return block != null;
        }

        /// <summary>
        /// HLSL file the Custom Function must source. Clock families →
        /// PrismClockAnimation.hlsl; erosion / back-face → PrismOcclusionCorridor.hlsl;
        /// sight → PrismDestructionSight.hlsl. Putting every CF's missing-message on
        /// PrismClockAnimation.hlsl was a lie for the non-clock families.
        /// </summary>
        public static string CustomFunctionSourceHint(string functionName)
        {
            switch (functionName)
            {
                case "PrismErosionFade":
                case "PrismBackFaceFade":
                case "PrismOcclusionFade":
                    return "PrismOcclusionCorridor.hlsl";
                case "PrismDestructionSight":
                    return "PrismDestructionSight.hlsl";
                default:
                    return "PrismClockAnimation.hlsl";
            }
        }

        public static string ExpectedCustomFunctionSourceGuid(string functionName)
        {
            switch (functionName)
            {
                case "PrismErosionFade":
                case "PrismBackFaceFade":
                case "PrismOcclusionFade":
                    return PrismOcclusionWiringValidator.CorridorHlslGuid;
                case "PrismDestructionSight":
                    return SightHlslGuid;
                default:
                    return ClockHlslGuid;
            }
        }

        public static bool GraphHasSpecifiedEdge(string[] blocks, GraphEdgeCheck check, out string fault)
        {
            fault = null;
            if (!TryFindCustomFunctionNodeId(blocks, check.InputFunction, out var inId))
            {
                fault = $"{check.Description}: Custom Function '{check.InputFunction}' missing";
                return false;
            }

            var incoming = ParseEdges(blocks[0])
                .Where(e => e.inNode == inId && e.inSlot == check.InputSlot)
                .ToList();
            if (incoming.Count == 0)
            {
                fault = $"{check.Description}: {check.InputFunction} slot {check.InputSlot} is unconnected";
                return false;
            }

            if (string.IsNullOrEmpty(check.OutputFunction))
                return true;

            if (!TryFindCustomFunctionNodeId(blocks, check.OutputFunction, out var outId))
            {
                fault = $"{check.Description}: output Custom Function '{check.OutputFunction}' missing";
                return false;
            }

            bool match = incoming.Any(e =>
                e.outNode == outId && (check.OutputSlot < 0 || e.outSlot == check.OutputSlot));
            if (!match)
            {
                fault = check.OutputSlot < 0
                    ? $"{check.Description}: {check.InputFunction} slot {check.InputSlot} is not fed by '{check.OutputFunction}'"
                    : $"{check.Description}: {check.InputFunction} slot {check.InputSlot} is not fed by '{check.OutputFunction}' slot {check.OutputSlot}";
                return false;
            }

            return true;
        }

        static bool TryFindCustomFunctionNodeId(string[] blocks, string fn, out string objectId)
        {
            objectId = null;
            var block = blocks.FirstOrDefault(b =>
                b.Contains("CustomFunctionNode") &&
                b.Contains($"\"m_FunctionName\": \"{fn}\""));
            if (block == null) return false;
            var m = Regex.Match(block, "\"m_ObjectId\":\\s*\"([0-9a-f]+)\"");
            if (!m.Success) return false;
            objectId = m.Groups[1].Value;
            return true;
        }

        static List<(string outNode, int outSlot, string inNode, int inSlot)> ParseEdges(string graphData)
        {
            var edges = new List<(string, int, string, int)>();
            foreach (Match m in EdgeRegex.Matches(graphData ?? string.Empty))
            {
                edges.Add((
                    m.Groups[1].Value,
                    int.Parse(m.Groups[2].Value),
                    m.Groups[3].Value,
                    int.Parse(m.Groups[4].Value)));
            }
            return edges;
        }

        static bool CheckUnexposedGlobal(StringBuilder report, string[] blocks, string prop, string tag)
        {
            if (!TryFindPropertyBlock(blocks, prop, out var block))
            {
                report.AppendLine($"   ❌ property {prop} MISSING ({tag})");
                return false;
            }
            if (block.Contains("\"m_GeneratePropertyBlock\": true"))
            {
                report.AppendLine($"   ❌ {prop} is EXPOSED — it must be unexposed (a global) or Shader.SetGlobal* cannot drive it");
                return false;
            }
            if (block.Contains(HybridPerInstanceOverride))
            {
                report.AppendLine($"   ❌ {prop} is Hybrid Per Instance — it is ONE value for the whole frame, not per prism");
                return false;
            }
            report.AppendLine($"   ✅ property {prop} (global, unexposed)");
            return true;
        }

        static bool CheckCustomFunction(StringBuilder report, string[] blocks, string text, string fn)
        {
            var block = blocks.FirstOrDefault(b =>
                b.Contains("CustomFunctionNode") &&
                b.Contains($"\"m_FunctionName\": \"{fn}\""));
            if (block == null && !text.Contains($"\"m_FunctionName\": \"{fn}\""))
            {
                report.AppendLine($"   ❌ Custom Function node '{fn}' NOT found (add it per §4.4, Source = {CustomFunctionSourceHint(fn)})");
                return false;
            }

            string guid = ExpectedCustomFunctionSourceGuid(fn);
            string sourceBlock = block ?? text;
            if (!string.IsNullOrEmpty(guid) && !sourceBlock.Contains($"\"m_FunctionSource\": \"{guid}\""))
            {
                report.AppendLine($"   ❌ Custom Function node '{fn}' present but sources the wrong HLSL (want {CustomFunctionSourceHint(fn)})");
                return false;
            }

            report.AppendLine($"   ✅ Custom Function node '{fn}' present");
            return true;
        }

        static bool CheckProp(StringBuilder report, string[] blocks, string prop, bool required)
        {
            string tag = required ? "required" : "optional (transparent-prism grow bloom)";
            if (!TryFindPropertyBlock(blocks, prop, out var block))
            {
                report.AppendLine(required
                    ? $"   ❌ property {prop} MISSING ({tag})"
                    : $"   ⚠ property {prop} not present ({tag})");
                return false;
            }

            bool hpi = block.Contains(HybridPerInstanceOverride);
            if (!hpi)
            {
                report.AppendLine($"   ❌ property {prop} exists but is NOT 'Hybrid Per Instance' (Node Settings ▸ Shader Declaration) — per-instance stamps will not reach the shader");
                return false;
            }

            report.AppendLine($"   ✅ property {prop} (Hybrid Per Instance)");
            return true;
        }

        static GraphEdgeCheck[] Concat(GraphEdgeCheck[] a, GraphEdgeCheck[] b)
        {
            var result = new GraphEdgeCheck[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }

        public static string FindGraphPath(string graphName)
        {
            string[] known =
            {
                $"Assets/_Graphics/Materials/Graphs/{graphName}.shadergraph",
                $"Assets/_Graphics/Materials/Graphs/PrismGraphs/{graphName}.shadergraph",
            };
            foreach (var p in known)
                if (File.Exists(p)) return p;

            return AssetDatabase.FindAssets(graphName)
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith($"{graphName}.shadergraph"));
        }
    }
}
