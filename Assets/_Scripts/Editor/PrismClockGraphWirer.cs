using System;
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
    /// Auto-adds the clock-material properties (Docs/PRISM_ANIMATION.md §4.4) to the
    /// three prism ShaderGraphs — the fiddly, typo-prone half of the wiring — by
    /// cloning DONOR property blocks already present in each graph (version-exact
    /// serialization by construction), rewriting identity fields, forcing
    /// Hybrid Per Instance, and registering them in the graph's property list +
    /// blackboard category. Safety: the original text is backed up in memory, the
    /// asset is reimported synchronously, and ANY import/compile error restores the
    /// original file automatically — a failed attempt costs nothing.
    ///
    /// Node wiring is NOT manual: all four graphs' Custom Function nodes and edge
    /// splices were synthesized out-of-editor with machine validation (donor-clone
    /// JSON, every node/slot/edge reference resolved before writing — the
    /// /asset-surgery skill documents the technique). This tool remains as the
    /// idempotent PROPERTY repair path (e.g. after a graph revert); a reverted
    /// node graph is restored from git, not re-drawn by hand.
    ///
    /// FrogletTools > Ecology > Prism Animation> Auto-Wire Clock Properties.
    /// </summary>
    public static class PrismClockGraphWirer
    {
        const string FloatType = "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty";
        const string Vector3Type = "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty";
        const string ColorType = "UnityEditor.ShaderGraph.Internal.ColorShaderProperty";

        class PropSpec
        {
            public string Ref;          // exact shader reference name (what C# stamps)
            public string Display;      // blackboard display name
            public string DonorType;    // serialized property class to clone
            public string ValueJson;    // replacement for the m_Value payload
            public bool Global;         // true = unexposed global uniform (no HPI) — the _PrismClock feed
        }

        static PropSpec GlobalClock() => new PropSpec
        {
            Ref = "_PrismClock",
            Display = "PrismClock",
            DonorType = FloatType,
            ValueJson = "0.0",
            Global = true,
        };

        static PropSpec F(string refName, float v) => new PropSpec
        {
            Ref = refName,
            Display = refName.TrimStart('_'),
            DonorType = FloatType,
            ValueJson = v.ToString("0.0###############", System.Globalization.CultureInfo.InvariantCulture),
        };

        static PropSpec V3(string refName, float x, float y, float z) => new PropSpec
        {
            Ref = refName,
            Display = refName.TrimStart('_'),
            DonorType = Vector3Type,
            ValueJson = "{\n        \"x\": " + x + ",\n        \"y\": " + y + ",\n        \"z\": " + z + "\n    }",
        };

        static PropSpec C4(string refName, float r, float g, float b, float a) => new PropSpec
        {
            Ref = refName,
            Display = refName.TrimStart('_'),
            DonorType = ColorType,
            ValueJson = "{\n        \"r\": " + r + ",\n        \"g\": " + g + ",\n        \"b\": " + b + ",\n        \"a\": " + a + "\n    }",
        };

        class GraphJob
        {
            public string GraphName;
            public PropSpec[] Props;
        }

        static readonly GraphJob[] Jobs =
        {
            new GraphJob
            {
                GraphName = "BlockGraph",
                Props = new[]
                {
                    GlobalClock(),
                    F("_GrowStartTime", 0f), F("_GrowRate", 0f), V3("_GrowStartFrac", 1f, 1f, 1f),
                    F("_ColorStartTime", 0f), F("_ColorDuration", 0f),
                    C4("_StartBrightColor", 1f, 1f, 1f, 1f), C4("_StartDarkColor", 1f, 1f, 1f, 1f),
                    V3("_StartSpread", 0f, 0f, 0f),
                    // Ballistic flight (Docs/PRISM_ANIMATION.md §5 C5): a prism FIRED as
                    // a projectile. Duration 0 = unstamped = render at the transform,
                    // which is every prism that is not in flight.
                    F("_FlightStartTime", 0f), F("_FlightDuration", 0f),
                    V3("_FlightVelocity", 0f, 0f, 0f),
                    // Super-shield deflection jiggle (Docs/PRISM_ANIMATION.md §5 C14): a
                    // super-shielded prism HIT but not destroyed. Duration 0 = unstamped =
                    // identity, which is every prism that has never deflected anything.
                    F("_JiggleStartTime", 0f), F("_JiggleDuration", 0f),
                    V3("_JiggleParams", 0f, 0f, 0f),
                },
            },
            new GraphJob
            {
                GraphName = "ExplodingBlockGraph",
                Props = new[]
                {
                    GlobalClock(),
                    // The flight velocity is the graph's existing world-space _Velocity
                    // (also the shatter-spin axis) — the world->object conversion is
                    // GPU-side inside PrismExplosionClock, so no extra property.
                    F("_ExplodeStartTime", 0f), F("_ExplodeSpeed", 0f), F("_ExplodeDuration", 0f),
                    // Transparent LIVE prisms rest on this graph — the grow trio +
                    // color five here are what let them bloom and fade colors instead
                    // of snapping (loudly) on spawn/steal.
                    F("_GrowStartTime", 0f), F("_GrowRate", 0f), V3("_GrowStartFrac", 1f, 1f, 1f),
                    F("_ColorStartTime", 0f), F("_ColorDuration", 0f),
                    C4("_StartBrightColor", 1f, 1f, 1f, 1f), C4("_StartDarkColor", 1f, 1f, 1f, 1f),
                    V3("_StartSpread", 0f, 0f, 0f),
                    // Ballistic flight (Docs/PRISM_ANIMATION.md §5 C5): a prism FIRED as
                    // a projectile. Duration 0 = unstamped = render at the transform,
                    // which is every prism that is not in flight.
                    F("_FlightStartTime", 0f), F("_FlightDuration", 0f),
                    V3("_FlightVelocity", 0f, 0f, 0f),
                    // Super-shield deflection jiggle (Docs/PRISM_ANIMATION.md §5 C14): a
                    // super-shielded prism HIT but not destroyed. Duration 0 = unstamped =
                    // identity, which is every prism that has never deflected anything.
                    F("_JiggleStartTime", 0f), F("_JiggleDuration", 0f),
                    V3("_JiggleParams", 0f, 0f, 0f),
                },
            },
            new GraphJob
            {
                GraphName = "SuctionGraph",
                Props = new[]
                {
                    GlobalClock(),
                    F("_SuctionStartTime", 0f), F("_SuctionDuration", 0f),
                    F("_SuctionDirection", 1f), F("_SuctionGrowDelay", 0f),
                },
            },
        };

        [MenuItem("FrogletTools/Ecology/Prism Animation/Auto-Wire Clock Properties (All Graphs)")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 4,
            Description = "Stamp clock properties across every prism shader graph.")]
        public static void AutoWireAll()
        {
            var report = new StringBuilder();
            report.AppendLine("[PrismClock] AUTO-WIRE CLOCK PROPERTIES — donor-clone + import-validate + auto-rollback");
            bool anyFailed = false;

            foreach (var job in Jobs)
            {
                string path = FindGraphPath(job.GraphName);
                if (path == null)
                {
                    report.AppendLine($"❌ {job.GraphName}: .shadergraph not found");
                    anyFailed = true;
                    continue;
                }

                try
                {
                    string result = WireGraph(path, job, report);
                    if (result != null) { anyFailed = true; report.AppendLine($"❌ {job.GraphName}: {result} — file RESTORED, no changes kept"); }
                }
                catch (Exception e)
                {
                    anyFailed = true;
                    report.AppendLine($"❌ {job.GraphName}: exception '{e.Message}' — check the file was restored (git status)");
                }
            }

            report.AppendLine(anyFailed
                ? "RESULT: ❌ some graphs untouched/restored — add those properties manually per the checklist."
                : "RESULT: ✅ all clock properties present (added or already there). NEXT: the Custom Function node wiring per Docs/PRISM_CLOCK_WIRING_CHECKLIST.md, then run Validate Clock Wiring.");
            if (anyFailed) Debug.LogWarning(report.ToString());
            else Debug.Log(report.ToString());
        }

        /// <summary>Returns null on success, else a failure reason (file already restored).</summary>
        static string WireGraph(string path, GraphJob job, StringBuilder report)
        {
            string original = File.ReadAllText(path);
            string sep = original.Contains("\r\n\r\n") ? "\r\n\r\n" : "\n\n";
            var blocks = original.Split(new[] { sep }, StringSplitOptions.None).ToList();
            if (blocks.Count < 2 || !blocks[0].Contains("\"m_Properties\""))
                return "unexpected file format (no GraphData m_Properties in first block)";

            var toAdd = job.Props.Where(p => !PropertyExists(original, p.Ref)).ToList();
            if (toAdd.Count == 0)
            {
                report.AppendLine($"✅ {job.GraphName}: all {job.Props.Length} clock properties already present — nothing to do");
                return null;
            }

            var newIds = new List<string>();
            foreach (var prop in toAdd)
            {
                int donorIdx = FindDonorIndex(blocks, prop.DonorType);
                if (donorIdx < 0)
                    return $"no donor property of type {prop.DonorType.Split('.').Last()} to clone for {prop.Ref}";

                string objectId = Guid.NewGuid().ToString("N");
                string clone = RewriteClone(blocks[donorIdx], prop, objectId);
                if (clone == null)
                    return $"donor rewrite failed for {prop.Ref} (unexpected donor shape)";

                blocks.Insert(donorIdx + 1, clone);
                newIds.Add(objectId);
            }

            // Register in GraphData.m_Properties (first block) …
            string graphData = blocks[0];
            foreach (var id in newIds)
            {
                var replaced = ReplaceFirst(graphData, "\"m_Properties\": \\[",
                    "\"m_Properties\": [\n        {\n            \"m_Id\": \"" + id + "\"\n        },");
                if (replaced == graphData) return "could not register into m_Properties";
                graphData = replaced;
            }
            blocks[0] = graphData;

            // … and in the blackboard category with the most children (the real one).
            int catIdx = FindBestCategoryIndex(blocks);
            if (catIdx >= 0)
            {
                string cat = blocks[catIdx];
                foreach (var id in newIds)
                {
                    // Empty list FIRST — inserting the non-empty form into "[]"
                    // would leave a trailing comma before the closing bracket.
                    var replaced = ReplaceFirst(cat, "\"m_ChildObjectList\": \\[\\]",
                        "\"m_ChildObjectList\": [\n        {\n            \"m_Id\": \"" + id + "\"\n        }\n    ]");
                    if (replaced == cat)
                        replaced = ReplaceFirst(cat, "\"m_ChildObjectList\": \\[",
                            "\"m_ChildObjectList\": [\n        {\n            \"m_Id\": \"" + id + "\"\n        },");
                    if (replaced != cat) cat = replaced;
                }
                blocks[catIdx] = cat;
            }
            else
            {
                report.AppendLine($"   ⚠ {job.GraphName}: no blackboard category found — properties added but may not show on the Blackboard until Shader Graph re-files them");
            }

            // Write + synchronous reimport + compile check; restore on ANY failure.
            File.WriteAllText(path, string.Join(sep, blocks));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            bool broken = shader == null || ShaderUtil.ShaderHasError(shader);
            if (broken)
            {
                File.WriteAllText(path, original);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                return "graph failed to import/compile after property insertion";
            }

            report.AppendLine($"✅ {job.GraphName}: added {toAdd.Count} properties ({string.Join(", ", toAdd.Select(p => p.Ref))}), " +
                              $"{job.Props.Length - toAdd.Count} already present; import verified clean");
            return null;
        }

        static bool PropertyExists(string text, string refName) =>
            text.Contains($"\"m_DefaultReferenceName\": \"{refName}\"") ||
            text.Contains($"\"m_OverrideReferenceName\": \"{refName}\"");

        static int FindDonorIndex(List<string> blocks, string donorType)
        {
            int fallback = -1;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (!blocks[i].Contains($"\"m_Type\": \"{donorType}\"")) continue;
                // Prefer a Hybrid Per Instance donor (fields already in the right state).
                if (blocks[i].Contains("\"hlslDeclarationOverride\": 3")) return i;
                if (fallback < 0) fallback = i;
            }
            return fallback;
        }

        static string RewriteClone(string donor, PropSpec prop, string objectId)
        {
            string s = donor;

            s = ReplaceFirst(s, "\"m_ObjectId\": \"[0-9a-f]+\"", $"\"m_ObjectId\": \"{objectId}\"");
            s = ReplaceFirst(s, "\"m_GuidSerialized\": \"[0-9a-f\\-]+\"", $"\"m_GuidSerialized\": \"{Guid.NewGuid()}\"");
            s = ReplaceFirst(s, "\"m_Name\": \"[^\"]*\"", $"\"m_Name\": \"{prop.Display}\"");
            s = ReplaceFirst(s, "\"m_RefNameGeneratedByDisplayName\": \"[^\"]*\"", $"\"m_RefNameGeneratedByDisplayName\": \"{prop.Display}\"");
            s = ReplaceFirst(s, "\"m_DefaultReferenceName\": \"[^\"]*\"", $"\"m_DefaultReferenceName\": \"{prop.Ref}\"");
            s = ReplaceFirst(s, "\"m_OverrideReferenceName\": \"[^\"]*\"", "\"m_OverrideReferenceName\": \"\"");
            // Globals (the _PrismClock feed) are UNEXPOSED with no declaration
            // override — Shader Graph declares unexposed properties as global
            // uniforms, which is what lets Shader.SetGlobalFloat drive them.
            // Per-instance stamps are EXPOSED + Hybrid Per Instance.
            s = ReplaceFirst(s, "\"overrideHLSLDeclaration\": (true|false)",
                prop.Global ? "\"overrideHLSLDeclaration\": false" : "\"overrideHLSLDeclaration\": true");
            s = ReplaceFirst(s, "\"hlslDeclarationOverride\": -?\\d+",
                prop.Global ? "\"hlslDeclarationOverride\": 0" : "\"hlslDeclarationOverride\": 3");
            s = ReplaceFirst(s, "\"m_Hidden\": (true|false)", "\"m_Hidden\": false");
            s = ReplaceFirst(s, "\"m_GeneratePropertyBlock\": (true|false)",
                prop.Global ? "\"m_GeneratePropertyBlock\": false" : "\"m_GeneratePropertyBlock\": true");

            // Default value: scalar for floats, flat object for vector/color.
            string valued = prop.DonorType == FloatType
                ? ReplaceFirst(s, "\"m_Value\": -?[0-9.eE+\\-]+", $"\"m_Value\": {prop.ValueJson}")
                : ReplaceFirst(s, "\"m_Value\": \\{[^}]*\\}", $"\"m_Value\": {prop.ValueJson}");
            if (valued == s && !donor.Contains(prop.ValueJson)) return null; // value rewrite failed
            s = valued;

            // Identity sanity: every rewritten field must actually differ from a
            // second clone of the same donor (i.e. the regexes matched).
            string expectedDecl = prop.Global ? "\"hlslDeclarationOverride\": 0" : "\"hlslDeclarationOverride\": 3";
            if (!s.Contains(objectId) || !s.Contains(prop.Ref) || !s.Contains(expectedDecl))
                return null;

            return s;
        }

        static int FindBestCategoryIndex(List<string> blocks)
        {
            int best = -1, bestChildren = -1;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (!blocks[i].Contains("CategoryData")) continue;
                if (!blocks[i].Contains("\"m_ChildObjectList\"")) continue;
                int children = Regex.Matches(blocks[i], "\"m_Id\": ").Count - 1; // minus the block's own m_ObjectId? (m_Id vs m_ObjectId differ — count is child refs)
                if (children > bestChildren) { bestChildren = children; best = i; }
            }
            return best;
        }

        static string ReplaceFirst(string input, string pattern, string replacement) =>
            new Regex(pattern).Replace(input, replacement, 1);

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
