using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    /// ground truth the runtime PrismClockDiagnostics use). Run after every wiring
    /// step — the report enumerates exactly what remains.
    /// FrogletTools > Ecology > Prism Animation> Validate Clock Wiring.
    /// </summary>
    public static class PrismClockWiringValidator
    {
        class GraphSpec
        {
            public string GraphName;
            public string[] RequiredProps;
            public string[] OptionalProps;
            public string Purpose;
        }

        static readonly GraphSpec[] Specs =
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
                },
                OptionalProps = new string[0],
                Purpose = "grow-in bloom (PrismGrowScale, vertex) + color/state transitions (PrismColorLerp, fragment) + ballistic flight (PrismFlightClock, vertex) + shield engage/shatter morph (PrismShieldMorph, vertex) + super-shield deflection jiggle (PrismJiggleClock, vertex)",
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
                },
                OptionalProps = new string[0],
                Purpose = "explosion debris flight/shatter/fade (PrismExplosionClock) + transparent live prism bloom/color/flight/shield morph/deflection",
            },
            new GraphSpec
            {
                GraphName = "SuctionGraph",
                RequiredProps = new[] { "_SuctionStartTime", "_SuctionDuration", "_SuctionDirection", "_SuctionGrowDelay" },
                OptionalProps = new string[0],
                Purpose = "implosion / reverse-grow suction (PrismSuctionClock)",
            },
        };

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
                var clockBlock = blocks.FirstOrDefault(b =>
                    b.Contains("\"m_DefaultReferenceName\": \"_PrismClock\"") && b.Contains("ShaderProperty"));
                if (clockBlock == null)
                {
                    report.AppendLine("   ❌ property _PrismClock MISSING (the global clock feed)");
                    allRequiredPass = false;
                }
                else if (clockBlock.Contains("\"m_GeneratePropertyBlock\": true"))
                {
                    report.AppendLine("   ❌ _PrismClock is EXPOSED — it must be unexposed (global uniform) or Shader.SetGlobalFloat cannot drive it");
                    allRequiredPass = false;
                }
                else
                    report.AppendLine("   ✅ property _PrismClock (global, unexposed)");

                // The Custom Function nodes reference PrismClockAnimation.hlsl by asset;
                // its function names appearing in the graph source is the cheapest
                // node-presence signal available textually.
                string expectedFunction = spec.GraphName == "BlockGraph" ? "PrismGrowScale"
                    : spec.GraphName == "ExplodingBlockGraph" ? "PrismExplosionClock"
                    : "PrismSuctionClock";
                if (text.Contains(expectedFunction))
                    report.AppendLine($"   ✅ Custom Function node '{expectedFunction}' present");
                else
                {
                    report.AppendLine($"   ❌ Custom Function node '{expectedFunction}' NOT found (add it per §4.4, Source = PrismClockAnimation.hlsl)");
                    allRequiredPass = false;
                }
                // Secondary CF nodes: BlockGraph carries the color lerp;
                // ExplodingBlockGraph carries the grow + color clusters too
                // (transparent live prisms bloom and fade colors on this graph).
                // BlockGraph also carries PrismFlightSqrDistance — the flight-corrected
                // camera distance that replaced the pivot-based SqrDistanceSubGraph feed
                // (a flying prism's pivot is parked at the flight end point, so the
                // distance-driven spread read full-range for the whole flight).
                string[] extraFunctions = spec.GraphName == "BlockGraph"
                    ? new[] { "PrismColorLerp", "PrismFlightClock", "PrismFlightSqrDistance", "PrismShieldMorph", "PrismJiggleClock" }
                    : spec.GraphName == "ExplodingBlockGraph"
                        ? new[] { "PrismGrowScale", "PrismColorLerp", "PrismFlightClock", "PrismShieldMorph", "PrismJiggleClock" }
                        : new string[0];
                foreach (var fn in extraFunctions)
                {
                    if (text.Contains(fn))
                        report.AppendLine($"   ✅ Custom Function node '{fn}' present");
                    else
                    {
                        report.AppendLine($"   ❌ Custom Function node '{fn}' NOT found (add it per §4.4, Source = PrismClockAnimation.hlsl)");
                        allRequiredPass = false;
                    }
                }
            }

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

        static bool CheckProp(StringBuilder report, string[] blocks, string prop, bool required)
        {
            // A property block carries its reference name (default or override) and
            // the Hybrid Per Instance flag in one serialized object.
            var block = blocks.FirstOrDefault(b =>
                (b.Contains($"\"m_DefaultReferenceName\": \"{prop}\"") ||
                 b.Contains($"\"m_OverrideReferenceName\": \"{prop}\"")) &&
                b.Contains("ShaderProperty"));

            string tag = required ? "required" : "optional (transparent-prism grow bloom)";
            if (block == null)
            {
                report.AppendLine(required
                    ? $"   ❌ property {prop} MISSING ({tag})"
                    : $"   ⚠ property {prop} not present ({tag})");
                return false;
            }

            bool hpi = block.Contains("\"hlslDeclarationOverride\": 3");
            if (!hpi)
            {
                report.AppendLine($"   ❌ property {prop} exists but is NOT 'Hybrid Per Instance' (Node Settings ▸ Shader Declaration) — per-instance stamps will not reach the shader");
                return false;
            }

            report.AppendLine($"   ✅ property {prop} (Hybrid Per Instance)");
            return true;
        }

        static string FindGraphPath(string graphName)
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
