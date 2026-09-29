using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>A single unapplied property override on a prefab instance in a scene.</summary>
    public sealed class PrefabOverrideRecord
    {
        public long TargetFileId;
        public string TargetGuid;     // guid of the prefab the target object belongs to (self or nested)
        public string PropertyPath;
        public string Value;
        public string ObjectRefGuid;  // non-null when the override points at an asset
        public long ObjectRefFileId;

        /// <summary>Identity of the overridden property, stable across scenes.</summary>
        public string Key => $"{TargetGuid}|{TargetFileId}|{PropertyPath}";

        /// <summary>
        /// Identity of the override's VALUE. Scene-object references can't be compared across
        /// scenes (different fileIDs for the same logical object), so they collapse to a marker -
        /// two scenes pointing at "their own controller" count as the same intent.
        /// </summary>
        public string ValueKey =>
            ObjectRefFileId != 0
                ? (ObjectRefGuid != null ? $"asset:{ObjectRefGuid}:{ObjectRefFileId}" : "sceneref")
                : $"v:{Value}";

        public override string ToString() => $"{PropertyPath} = {Value}";
    }

    /// <summary>One prefab instance found inside one scene.</summary>
    public sealed class ScenePrefabInstance
    {
        public string ScenePath;
        public string SceneName => Path.GetFileNameWithoutExtension(ScenePath);
        public long InstanceFileId;
        public string SourcePrefabGuid;
        public List<PrefabOverrideRecord> Overrides = new();
        public int RemovedComponents;
        public int RemovedGameObjects;
        public int AddedGameObjects;
        public int AddedComponents;

        public string RootNameOverride =>
            Overrides.FirstOrDefault(o => o.PropertyPath == "m_Name")?.Value;

        public int StructuralChanges =>
            RemovedComponents + RemovedGameObjects + AddedGameObjects + AddedComponents;
    }

    /// <summary>
    /// Reads prefab-instance overrides straight out of scene YAML, without opening the scenes.
    ///
    /// This is what makes "is any scene running an unsaved version of this prefab?" answerable in
    /// under a second across the whole project: opening 16 scenes to ask <c>PrefabUtility</c> the
    /// same question costs minutes and mutates the editor's scene setup. The parser only needs the
    /// <c>PrefabInstance</c> documents, whose shape is fixed by Unity's serializer.
    ///
    /// Reading is advisory - every WRITE still goes through <c>PrefabUtility</c> on a properly
    /// loaded scene (see <see cref="PrefabDriftFixer"/>), so Unity owns the serialization.
    /// </summary>
    public static class PrefabInstanceSceneScanner
    {
        static readonly Regex SourcePrefabRe =
            new(@"m_SourcePrefab:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);

        static readonly Regex TargetRe =
            new(@"^-\s*target:\s*\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-f]{32}))?", RegexOptions.Compiled);

        static readonly Regex ObjRefRe =
            new(@"objectReference:\s*\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-f]{32}))?", RegexOptions.Compiled);

        // ── Scene discovery ──────────────────────────────────────────────────────

        public static List<string> FindScenes(IEnumerable<string> folders, IEnumerable<string> excludeFragments = null)
        {
            var roots = folders?.Where(AssetDatabase.IsValidFolder).ToArray();
            var guids = roots is { Length: > 0 }
                ? AssetDatabase.FindAssets("t:Scene", roots)
                : AssetDatabase.FindAssets("t:Scene");

            var excl = excludeFragments?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();

            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p) && p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .Where(p => !excl.Any(x => p.Contains(x, StringComparison.OrdinalIgnoreCase)))
                .Distinct()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ── Scanning ─────────────────────────────────────────────────────────────

        /// <summary>Every instance of the given prefab GUIDs across the given scenes.</summary>
        public static List<ScenePrefabInstance> ScanScenes(IEnumerable<string> scenePaths, ISet<string> prefabGuids)
        {
            var results = new List<ScenePrefabInstance>();
            foreach (var sp in scenePaths)
            {
                try { results.AddRange(ScanScene(sp, prefabGuids)); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PrefabKit] Could not scan '{sp}': {e.Message}");
                }
            }
            return results;
        }

        public static List<ScenePrefabInstance> ScanScene(string scenePath, ISet<string> prefabGuids)
        {
            var found = new List<ScenePrefabInstance>();
            var full = Path.Combine(Directory.GetCurrentDirectory(), scenePath);
            if (!File.Exists(full)) return found;

            var text = File.ReadAllText(full);
            // Cheap bail-out: no referenced guid appears anywhere in the file.
            if (prefabGuids != null && prefabGuids.Count > 0 && !prefabGuids.Any(g => text.Contains(g, StringComparison.Ordinal)))
                return found;

            foreach (var (fileId, body) in EnumerateDocuments(text, "PrefabInstance"))
            {
                var m = SourcePrefabRe.Match(body);
                if (!m.Success) continue;
                var guid = m.Groups[1].Value;
                if (prefabGuids != null && prefabGuids.Count > 0 && !prefabGuids.Contains(guid)) continue;

                var inst = ParseInstance(body);
                inst.ScenePath = scenePath;
                inst.InstanceFileId = fileId;
                inst.SourcePrefabGuid = guid;
                found.Add(inst);
            }
            return found;
        }

        // ── Document splitting ───────────────────────────────────────────────────

        /// <summary>
        /// Yields (fileID, body) for every YAML document whose type line matches <paramref name="typeName"/>.
        /// Line wrapping inside <c>{...}</c> is collapsed first so a flow map is always one line.
        /// </summary>
        static readonly Regex DocHeaderRe =
            new(@"^--- !u!\d+ &(-?\d+)", RegexOptions.Multiline | RegexOptions.Compiled);

        static IEnumerable<(long fileId, string body)> EnumerateDocuments(string text, string typeName)
        {
            var matches = DocHeaderRe.Matches(text);
            for (int i = 0; i < matches.Count; i++)
            {
                int start = matches[i].Index;
                int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var doc = text[start..end];

                int nl = doc.IndexOf('\n');
                if (nl < 0) continue;
                var afterHeader = doc[(nl + 1)..];
                if (!afterHeader.TrimStart().StartsWith(typeName + ":", StringComparison.Ordinal)) continue;

                if (!long.TryParse(matches[i].Groups[1].Value, out var id)) continue;
                yield return (id, Unwrap(afterHeader));
            }
        }

        /// <summary>Collapses newlines that fall inside an open brace so each entry is single-line.</summary>
        static string Unwrap(string s)
        {
            var sb = new StringBuilder(s.Length);
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '{') depth++;
                else if (c == '}') depth = Math.Max(0, depth - 1);

                if (c == '\n' && depth > 0)
                {
                    sb.Append(' ');
                    while (i + 1 < s.Length && (s[i + 1] == ' ' || s[i + 1] == '\t')) i++;
                    continue;
                }
                if (c != '\r') sb.Append(c);
            }
            return sb.ToString();
        }

        // ── Instance body parsing ────────────────────────────────────────────────

        enum Section { None, Modifications, RemovedComponents, RemovedGameObjects, AddedGameObjects, AddedComponents }

        static ScenePrefabInstance ParseInstance(string body)
        {
            var inst = new ScenePrefabInstance();
            var section = Section.None;
            PrefabOverrideRecord current = null;

            foreach (var raw in body.Split('\n'))
            {
                var line = raw.TrimEnd();
                var trimmed = line.TrimStart();
                if (trimmed.Length == 0) continue;

                // Section headers under m_Modification. An empty list serialises inline
                // ("m_RemovedComponents: []"), which simply yields a section with no "- " rows.
                if (TrySection(trimmed, out var newSection))
                {
                    Flush(inst, ref current);
                    section = newSection;
                    continue;
                }

                switch (section)
                {
                    case Section.Modifications:
                        if (trimmed.StartsWith("- target:", StringComparison.Ordinal))
                        {
                            Flush(inst, ref current);
                            current = new PrefabOverrideRecord();
                            var tm = TargetRe.Match(trimmed);
                            if (tm.Success)
                            {
                                long.TryParse(tm.Groups[1].Value, out var tid);
                                current.TargetFileId = tid;
                                current.TargetGuid = tm.Groups[2].Success ? tm.Groups[2].Value : null;
                            }
                        }
                        else if (current != null && trimmed.StartsWith("propertyPath:", StringComparison.Ordinal))
                        {
                            current.PropertyPath = Unquote(trimmed["propertyPath:".Length..].Trim());
                        }
                        else if (current != null && trimmed.StartsWith("value:", StringComparison.Ordinal))
                        {
                            current.Value = Unquote(trimmed["value:".Length..].Trim());
                        }
                        else if (current != null && trimmed.StartsWith("objectReference:", StringComparison.Ordinal))
                        {
                            var om = ObjRefRe.Match(trimmed);
                            if (om.Success)
                            {
                                long.TryParse(om.Groups[1].Value, out var oid);
                                current.ObjectRefFileId = oid;
                                current.ObjectRefGuid = om.Groups[2].Success ? om.Groups[2].Value : null;
                            }
                            Flush(inst, ref current);
                        }
                        break;

                    case Section.RemovedComponents:
                        if (trimmed.StartsWith("- ", StringComparison.Ordinal)) inst.RemovedComponents++;
                        break;
                    case Section.RemovedGameObjects:
                        if (trimmed.StartsWith("- ", StringComparison.Ordinal)) inst.RemovedGameObjects++;
                        break;
                    case Section.AddedGameObjects:
                        // Serialised as a multi-line record per entry; the "- " row starts each one.
                        if (trimmed.StartsWith("- ", StringComparison.Ordinal)) inst.AddedGameObjects++;
                        break;
                    case Section.AddedComponents:
                        if (trimmed.StartsWith("- ", StringComparison.Ordinal)) inst.AddedComponents++;
                        break;
                }
            }

            Flush(inst, ref current);
            return inst;
        }

        /// <summary>
        /// Unity quotes a scalar whenever YAML would otherwise mis-read it, so the SAME property
        /// serialises as <c>m_ActiveFontFeatures.Array.data[0]</c> in one scene and
        /// <c>'m_ActiveFontFeatures.Array.data[0]'</c> in another. Without stripping the quotes the
        /// two spellings hash differently and identical overrides look divergent.
        /// </summary>
        static string Unquote(string s)
        {
            if (s.Length >= 2 &&
                ((s[0] == '\'' && s[^1] == '\'') || (s[0] == '"' && s[^1] == '"')))
                return s[1..^1].Replace("''", "'");
            return s;
        }

        static bool TrySection(string trimmed, out Section section)
        {
            if (trimmed == "m_Modifications:") { section = Section.Modifications; return true; }
            if (trimmed.StartsWith("m_RemovedComponents:", StringComparison.Ordinal)) { section = Section.RemovedComponents; return true; }
            if (trimmed.StartsWith("m_RemovedGameObjects:", StringComparison.Ordinal)) { section = Section.RemovedGameObjects; return true; }
            if (trimmed.StartsWith("m_AddedGameObjects:", StringComparison.Ordinal)) { section = Section.AddedGameObjects; return true; }
            if (trimmed.StartsWith("m_AddedComponents:", StringComparison.Ordinal)) { section = Section.AddedComponents; return true; }
            if (trimmed.StartsWith("m_SourcePrefab:", StringComparison.Ordinal)) { section = Section.None; return true; }
            section = Section.None;
            return false;
        }

        static void Flush(ScenePrefabInstance inst, ref PrefabOverrideRecord current)
        {
            if (current is { PropertyPath: not null })
                inst.Overrides.Add(current);
            current = null;
        }

        // ── Cross-scene analysis ─────────────────────────────────────────────────

        /// <summary>
        /// Splits a prefab's overrides into the ones that are IDENTICAL in every scene that has
        /// them (so they belong applied to the prefab itself) and the ones whose value genuinely
        /// differs between scenes (real per-scene configuration).
        ///
        /// This is the distinction that turns "1,700 overrides per scene" from a wall of noise
        /// into a short, reviewable list.
        /// </summary>
        public static (List<string> uniform, List<string> divergent) ClassifyOverrides(
            IReadOnlyList<ScenePrefabInstance> instances, IEnumerable<string> ignoredPrefixes = null)
        {
            var ignore = ignoredPrefixes?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();
            var uniform = new List<string>();
            var divergent = new List<string>();
            if (instances == null || instances.Count < 2) return (uniform, divergent);

            var byKey = new Dictionary<string, HashSet<string>>();
            foreach (var inst in instances)
            foreach (var o in inst.Overrides)
            {
                if (o.PropertyPath == null) continue;
                if (ignore.Any(p => o.PropertyPath.StartsWith(p, StringComparison.Ordinal))) continue;
                if (!byKey.TryGetValue(o.Key, out var set))
                    byKey[o.Key] = set = new HashSet<string>();
                set.Add(o.ValueKey);
            }

            foreach (var (key, values) in byKey)
            {
                if (values.Count == 1) uniform.Add(key);
                else divergent.Add(key);
            }
            return (uniform, divergent);
        }

        /// <summary>Overrides on this instance that are not pure per-scene layout noise.</summary>
        public static int MeaningfulOverrideCount(ScenePrefabInstance inst, IEnumerable<string> ignoredPrefixes)
        {
            var ignore = ignoredPrefixes?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();
            return inst.Overrides.Count(o =>
                o.PropertyPath != null &&
                !ignore.Any(p => o.PropertyPath.StartsWith(p, StringComparison.Ordinal)));
        }
    }
}
