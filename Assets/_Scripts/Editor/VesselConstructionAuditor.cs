using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The three structural checks whose ABSENCE let each discrepancy in
    /// <c>Docs/VESSEL_CONSTRUCTION.md</c> through. All asset-only, no play mode.
    ///
    /// <list type="number">
    /// <item><b>Guid ownership</b> (§2). Exactly one <c>.meta</c> OWNS a guid — the file whose own
    /// top-level <c>guid:</c> line carries it. Every other hit is a REFERENCE, and a model's
    /// <c>.meta</c> is a completely ordinary place for another model's guid to appear (an FBX can
    /// carry an <c>externalObjects</c> material remap into a different FBX). Resolving with
    /// <c>grep -rl … | head -1</c> picks by filename order, which put two passes of Rhino jet
    /// placement on a placeholder hull a fifth of the ship's height.</item>
    ///
    /// <item><b>Nested-instance reachability</b> (§3). A prefab instance's parenting lives in
    /// <c>m_TransformParent</c> ALWAYS, plus an entry in the parent Transform's <c>m_Children</c>
    /// <b>iff that parent is a plain (non-stripped) Transform</b>. Generalising "no entry needed"
    /// from the Squirrel's jets — whose parent is stripped and structurally CANNOT carry one —
    /// shipped eight Rhino jets that were in the file and not in the hierarchy.</item>
    ///
    /// <item><b>Coincident duplicate renderers</b> (§3.4). Two <c>SkinnedMeshRenderer</c>s drawing
    /// the same hull from two files is a duplicate draw, and it makes the morph audit see eight
    /// element shapes where the contract says four.</item>
    /// </list>
    ///
    /// <para>Checks 1 and 3 use Unity's own object graph. Check 2 reads the prefab YAML directly,
    /// and has to: a loaded prefab presents the MERGED hierarchy, so an instance that is missing
    /// from its parent's children list looks perfectly attached once Unity has resolved it. The
    /// defect exists only in the file.</para>
    /// </summary>
    public static class VesselConstructionAuditor
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const string ModelFolder = "Assets/_Models/Vessel Models";

        [MenuItem("FrogletTools/Vessels/Audit Vessel Construction")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Guid ownership, nested-instance reachability and duplicate coincident " +
                          "hull renderers — the three checks VESSEL_CONSTRUCTION.md exists because of.")]
        public static void Audit()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Vessel construction audit ===");
            report.AppendLine();

            int problems = 0;
            problems += AuditGuidOwnership(report);
            problems += AuditNestedInstanceReachability(report);
            problems += AuditDuplicateHullRenderers(report);

            report.AppendLine();
            report.AppendLine(problems == 0
                ? "PASS — no ambiguous guid, no unreachable instance, no duplicate hull draw."
                : $"{problems} problem(s) found.");
            if (problems == 0) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        // ── 1. guid ownership ────────────────────────────────────────────────────

        static int AuditGuidOwnership(StringBuilder report)
        {
            report.AppendLine("--- guid ownership (VESSEL_CONSTRUCTION.md §2)");
            int bad = 0;
            foreach (var modelPath in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder })
                         .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p))
            {
                string guid = AssetDatabase.AssetPathToGUID(modelPath);
                var owners = OwnersOf(guid);
                if (owners.Count == 1 && owners[0] == modelPath) continue;
                bad++;
                report.AppendLine($"    ! {modelPath}");
                report.AppendLine($"        guid {guid} is DECLARED by {owners.Count} .meta file(s): " +
                                  string.Join(", ", owners));
            }
            report.AppendLine(bad == 0
                ? $"    OK — every vessel model's guid is declared by exactly its own .meta."
                : $"    {bad} model(s) with ambiguous ownership.");
            report.AppendLine();
            return bad;
        }

        /// <summary>
        /// Which files DECLARE this guid, by reading each `.meta`'s own top-level `guid:` line.
        /// A file that merely mentions the guid is a referrer and is deliberately not counted.
        /// </summary>
        public static List<string> OwnersOf(string guid)
        {
            var owners = new List<string>();
            if (string.IsNullOrEmpty(guid)) return owners;
            foreach (var metaFull in System.IO.Directory.GetFiles(
                         Application.dataPath, "*.meta", System.IO.SearchOption.AllDirectories))
            {
                string declared = null;
                foreach (var line in System.IO.File.ReadLines(metaFull))
                {
                    if (!line.StartsWith("guid:")) continue;
                    declared = line.Substring(5).Trim();
                    break;                                   // the FIRST guid: line is the file's own
                }
                if (declared != guid) continue;
                string rel = "Assets" + metaFull.Substring(Application.dataPath.Length)
                    .Replace('\\', '/');
                owners.Add(rel.Substring(0, rel.Length - 5));  // strip ".meta"
            }
            return owners;
        }

        // ── 2. nested-instance reachability ──────────────────────────────────────

        static int AuditNestedInstanceReachability(StringBuilder report)
        {
            report.AppendLine("--- nested prefab-instance reachability (VESSEL_CONSTRUCTION.md §3)");
            int bad = 0, plainParented = 0, strippedParented = 0;

            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder })
                         .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                string text;
                try { text = System.IO.File.ReadAllText(path); }
                catch { report.AppendLine($"    ! could not read {path}"); bad++; continue; }

                var docs = PrefabYaml.Split(text);
                foreach (var pi in docs.Where(d => d.TypeName == "PrefabInstance"))
                {
                    string parent = PrefabYaml.FirstFileId(PrefabYaml.Field(pi.Body, "m_TransformParent"));
                    if (string.IsNullOrEmpty(parent) || parent == "0")
                    {
                        // A root instance has no parent; that is legal and not what this checks.
                        continue;
                    }
                    var parentDoc = docs.FirstOrDefault(d => d.FileId == parent);
                    if (parentDoc == null) continue;          // parent lives in another asset
                    if (parentDoc.Stripped) { strippedParented++; continue; }  // cannot carry the entry

                    plainParented++;
                    // The instance appears in m_Children as its own STRIPPED Transform's fileID.
                    var strippedForThis = docs
                        .Where(d => d.Stripped &&
                                    PrefabYaml.FirstFileId(PrefabYaml.Field(d.Body, "m_PrefabInstance")) == pi.FileId)
                        .Select(d => d.FileId).ToList();
                    var children = PrefabYaml.ChildFileIds(parentDoc.Body);
                    if (strippedForThis.Any(children.Contains)) continue;

                    bad++;
                    report.AppendLine($"    ! {System.IO.Path.GetFileName(path)}: instance &{pi.FileId} " +
                                      $"({PrefabYaml.InstanceName(pi.Body) ?? "unnamed"}) names a PLAIN parent " +
                                      $"&{parent} but is ABSENT from its m_Children — it is in the file " +
                                      "and not in the hierarchy.");
                }
            }
            report.AppendLine($"    {plainParented} plain-parented instance(s), " +
                              $"{strippedParented} stripped-parented (structurally exempt), " +
                              $"{bad} unreachable.");
            report.AppendLine(bad == 0 ? "    OK — zero unreachable, which is the invariant."
                                       : "    ^ these render nowhere.");
            report.AppendLine();
            return bad;
        }

        // ── 3. duplicate coincident hull renderers ───────────────────────────────

        static int AuditDuplicateHullRenderers(StringBuilder report)
        {
            report.AppendLine("--- coincident duplicate hull renderers (VESSEL_CONSTRUCTION.md §3.4)");
            int bad = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder })
                         .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;

                var drawn = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(r => r && r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh)
                    .ToList();

                // Two renderers drawing meshes of the SAME vertex count from DIFFERENT model files
                // is the shape of the finding: the same export at two unit scales.
                foreach (var group in drawn.GroupBy(r => r.sharedMesh.vertexCount).Where(g => g.Count() > 1))
                {
                    var sources = group
                        .Select(r => AssetDatabase.GetAssetPath(r.sharedMesh))
                        .Distinct().ToList();
                    if (sources.Count < 2) continue;          // one file legitimately drawn twice is a different question
                    bad++;
                    report.AppendLine($"    ! {prefab.name}: {group.Count()} active SkinnedMeshRenderers " +
                                      $"drawing {group.Key}-vertex meshes from {sources.Count} different files:");
                    foreach (var r in group)
                        report.AppendLine($"        '{r.name}' <- {AssetDatabase.GetAssetPath(r.sharedMesh)}");
                }
            }
            report.AppendLine(bad == 0 ? "    OK — no vessel draws one hull twice."
                                       : $"    {bad} duplicate hull draw(s). Do not delete either " +
                                         "until it is established which is deliberate (§3.4).");
            report.AppendLine();
            return bad;
        }

        // ── a very small YAML reader, scoped to what these checks ask ────────────

        /// <summary>
        /// Just enough Unity-YAML to answer "is this instance listed in its parent's children".
        /// Deliberately NOT a general parser: the one question it exists for is invisible to the
        /// loaded object graph, and everything else here uses Unity's own API.
        /// </summary>
        static class PrefabYaml
        {
            public class Doc
            {
                public string FileId;
                public string TypeName;
                public bool Stripped;
                public string Body;
            }

            static readonly System.Text.RegularExpressions.Regex Header =
                new(@"^--- !u!(\d+) &(\d+)( stripped)?\s*$",
                    System.Text.RegularExpressions.RegexOptions.Multiline);

            public static List<Doc> Split(string text)
            {
                var docs = new List<Doc>();
                var matches = Header.Matches(text);
                for (int i = 0; i < matches.Count; i++)
                {
                    int start = matches[i].Index + matches[i].Length;
                    int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                    string body = text.Substring(start, end - start);
                    // The type name is the first "Word:" line of the body.
                    var typeMatch = System.Text.RegularExpressions.Regex.Match(body, @"^\s*(\w+):",
                        System.Text.RegularExpressions.RegexOptions.Multiline);
                    docs.Add(new Doc
                    {
                        FileId = matches[i].Groups[2].Value,
                        Stripped = matches[i].Groups[3].Success,
                        TypeName = typeMatch.Success ? typeMatch.Groups[1].Value : "?",
                        Body = body,
                    });
                }
                return docs;
            }

            public static string Field(string body, string key)
            {
                // `$` in .NET does not match before `\r`, so a CRLF checkout silently returns
                // nothing from every line-anchored pattern. Capture the ending instead.
                var m = System.Text.RegularExpressions.Regex.Match(
                    body, @"^\s*" + System.Text.RegularExpressions.Regex.Escape(key) + @":\s*(.*?)\r?$",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }

            public static string FirstFileId(string value)
            {
                if (value == null) return null;
                var m = System.Text.RegularExpressions.Regex.Match(value, @"fileID:\s*(-?\d+)");
                return m.Success ? m.Groups[1].Value : null;
            }

            public static List<string> ChildFileIds(string body)
            {
                var ids = new List<string>();
                var block = System.Text.RegularExpressions.Regex.Match(
                    body, @"m_Children:\s*\r?\n((?:\s*-\s*\{fileID:\s*-?\d+\}\s*\r?\n)*)");
                if (!block.Success) return ids;
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(block.Groups[1].Value, @"fileID:\s*(-?\d+)"))
                    ids.Add(m.Groups[1].Value);
                return ids;
            }

            public static string InstanceName(string body)
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    body, @"propertyPath:\s*m_Name\s*\r?\n\s*value:\s*(.*?)\r?$",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }
        }
    }
}
