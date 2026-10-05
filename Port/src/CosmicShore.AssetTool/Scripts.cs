using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Editing;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Yaml;

namespace CosmicShore.AssetTool
{
    /// <summary>The project's scripts, resolved against the compiled game code.</summary>
    static class Scripts
    {
        static AssetDatabase s_db;
        static ScriptTypeMap s_types;
        static ScriptCatalog s_catalog;

        public static AssetDatabase Db => s_db ??= new AssetDatabase(
            AssetDatabase.FindProjectRoot() ?? throw new ArgumentException("no Unity project found above the current directory"));

        public static ScriptTypeMap Types => s_types ??= new ScriptTypeMap(Db, new[]
        {
            typeof(CosmicShore.Core.AppManager).Assembly,       // the game (CosmicShore.Live)
            typeof(DG.Tweening.DOTween).Assembly,              // third-party shims (CosmicShore.Compat)
            typeof(CosmicShore.Engine.GameObject).Assembly,    // engine built-ins (uGUI, TMP, Netcode …)
        });

        public static ScriptCatalog Catalog => s_catalog ??= new ScriptCatalog(Db, Types);

        /// <summary>
        /// cs-asset schema [path…]: regenerate every script component / ScriptableObject in the
        /// given files from its C# type and compare the FIELD LAYOUT (names, order, shapes) with
        /// what Unity wrote. Values are not compared — a saved component holds tuned values,
        /// a new one holds defaults. A type counts as verified when at least one saved instance
        /// matches exactly; instances that differ only by missing/extra fields are STALE (saved
        /// before the script last changed), anything else is a serializer disagreement.
        /// </summary>
        public static int Schema(List<string> paths)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".unity", ".prefab", ".asset" };
            if (paths.Count == 0) paths.Add(Db.AssetsRoot);
            var files = paths.SelectMany(p => Directory.Exists(p)
                ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories).Where(f => exts.Contains(Path.GetExtension(f)))
                : new[] { p }).ToList();

            int instances = 0, exact = 0, stale = 0, shape = 0, unresolved = 0, toolFiles = 0, packageScripts = 0;
            var typeExact = new HashSet<Type>();
            var typeSeen = new HashSet<Type>();
            var shapeReasons = new Dictionary<string, (int count, string example)>();
            var typeShape = new Dictionary<Type, string>();
            foreach (var f in files)
            {
                string text = File.ReadAllText(f);
                if (!text.StartsWith("%YAML", StringComparison.Ordinal)) continue;
                // The repo's Python generators write "m_EditorClassIdentifier:" with no trailing
                // space; Unity never does. Their files are not evidence about Unity's serializer.
                if (text.Contains("m_EditorClassIdentifier:\n")) { toolFiles++; continue; }
                foreach (var d in UnityYaml.ParseDocuments(text))
                {
                    if (d.ClassId != 114 || d.Stripped) continue;
                    string guid = d.Body["m_Script"]?.Str("guid");
                    if (string.IsNullOrEmpty(guid)) continue;
                    var type = Types.Resolve(guid);
                    if (type == null) { unresolved++; continue; }
                    // Package scripts (uGUI, TMP …) resolve to the port's engine stand-ins, whose
                    // fields are not Unity's private layout; they are added from templates.
                    if (Catalog.FromGuid(guid).IsStandIn) { packageScripts++; continue; }
                    instances++;
                    typeSeen.Add(type);
                    var expected = new YMap();
                    foreach (var kv in ComponentSerializer.Fields(type, ComponentSerializer.NewInstance(type), 0)) expected.Add(kv.Key, kv.Value);
                    var actual = new YMap();
                    foreach (var e in d.Body.Entries)
                        if (!HeaderKeys.Contains(e.Key)) actual.Add(e.Key, e.Value);
                    var diff = Compare(expected, actual, type.Name);
                    if (diff == null) { exact++; typeExact.Add(type); continue; }
                    if (diff.Stale) { stale++; continue; }
                    shape++;
                    string key = diff.Kind + ": " + diff.Detail;
                    shapeReasons[key] = (shapeReasons.GetValueOrDefault(key).count + 1, Path.GetFileName(f) + " " + diff.Where);
                    typeShape.TryAdd(type, key);
                }
            }
            int typesVerified = typeExact.Count;
            int typesShapeOnly = typeShape.Keys.Count(t => !typeExact.Contains(t));
            Console.WriteLine($"{instances} script instances ({typeSeen.Count} types) in Unity-written files; {unresolved} with no resolvable script; {toolFiles} generator-written files and {packageScripts} package/plugin stand-in instances skipped");
            Console.WriteLine($"  layout identical to Unity's:           {exact}");
            Console.WriteLine($"  stale (fields added/removed since save): {stale}");
            Console.WriteLine($"  shape disagreement:                    {shape}");
            Console.WriteLine($"  types with >=1 identical instance:     {typesVerified}/{typeSeen.Count}");
            Console.WriteLine($"  types whose ONLY non-stale instances disagree in shape: {typesShapeOnly}");
            if (Environment.GetEnvironmentVariable("CS_SCHEMA_KEYS") == "1")
                foreach (var kv in KeyStats.OrderByDescending(k => k.Value).Take(40)) Console.WriteLine($"  key {kv.Value,6}  {kv.Key}");
            foreach (var kv in shapeReasons.OrderByDescending(k => k.Value.count).Take(40))
                Console.WriteLine($"  {kv.Value.count,5}  {kv.Key}   e.g. {kv.Value.example}");
            return 0;
        }

        /// <summary>
        /// cs-asset addall: add EVERY component script in the project, plus every built-in and
        /// package component, to a fresh GameObject each; the write must read back as the edit.
        /// A smoke test of the whole add path (requirements, templates, serializer).
        /// </summary>
        public static int AddAll()
        {
            var templates = new ComponentTemplates(Db.AssetsRoot);
            var names = new List<string>();
            foreach (var path in Db.AllAssetPaths)
            {
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                var info = Catalog.FromGuid(Db.GuidOf(path));
                if (info.Type == null || info.Type.IsAbstract || info.Type.IsGenericTypeDefinition) continue;
                if (!typeof(CosmicShore.Engine.Component).IsAssignableFrom(info.Type)) continue;
                if (path.Replace('\\', '/').Contains("/Editor/")) continue;
                // Add by the name a person would type: the script's own (file) name and namespace.
                names.Add(info.UnityFullName ?? info.Type.FullName);
            }
            names.AddRange(ComponentTemplates.BuiltInClassIds.Keys.Where(k => k is not ("Transform" or "RectTransform")));
            names.AddRange(ScriptTypeMap.PackageScripts.Values.Distinct());
            int ok = 0, refused = 0, failed = 0;
            var refusals = new Dictionary<string, int>();
            foreach (var name in names.Distinct())
            {
                var file = UnityYamlFile.Parse("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
                var ed = new UnityAssetEditor(file, seed: 1);
                long go = ed.CreateGameObject("Probe");
                try
                {
                    var r = new ComponentAdder(ed, Catalog, templates).Add(go, name);
                    if (!UnityYamlFile.SameContent(file, UnityYamlFile.Parse(file.Write()))) { failed++; Console.WriteLine($"  FAIL {name}: output does not read back"); continue; }
                    ok++;
                }
                catch (ArgumentException e)
                {
                    refused++;
                    string why = System.Text.RegularExpressions.Regex.Replace(e.Message, @"&-?\d+|'[^']*'|\b[A-Z]\w+\b(?= is)", "X");
                    refusals[why] = refusals.GetValueOrDefault(why) + 1;
                    if (Environment.GetEnvironmentVariable("CS_ADDALL_VERBOSE") == "1") Console.WriteLine($"  refused {name}: {e.Message}");
                }
                catch (Exception e) { failed++; Console.WriteLine($"  FAIL {name}: {e.GetType().Name}: {e.Message}"); }
            }
            Console.WriteLine($"{names.Distinct().Count()} components: {ok} added, {refused} refused (by a rule), {failed} failed");
            foreach (var kv in refusals.OrderByDescending(k => k.Value)) Console.WriteLine($"  {kv.Value,4} refused: {kv.Key}");
            return failed == 0 ? 0 : 1;
        }

        static readonly HashSet<string> HeaderKeys = new(StringComparer.Ordinal)
        {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject",
            "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier",
        };

        static readonly Dictionary<string, int> KeyStats = new();

        sealed record Diff(string Kind, string Detail, string Where, bool Stale);

        static Diff Compare(YNode exp, YNode act, string where)
        {
            switch (exp)
            {
                // An object reference is a reference whatever it points at ({fileID: 0} vs {fileID, guid, type}).
                case YMap er when er.Flow && er.Has("fileID") && act is YMap ar && ar.Flow && ar.Has("fileID"):
                    return null;
                case YMap em when act is YMap am:
                {
                    if (em.Entries.Count > 0 && am.Entries.Count > 0 && em.Flow != am.Flow)
                        return new Diff("style", (em.Flow ? "flow" : "block") + " vs file " + (am.Flow ? "flow" : "block"), where, false);
                    var ek = em.Entries.Select(e => e.Key).ToList();
                    var ak = am.Entries.Select(e => e.Key).ToList();
                    Diff staleDiff = null;
                    var missing = ek.Except(ak).ToList();
                    var extra = ak.Except(ek).ToList();
                    foreach (var k in missing) KeyStats[$"code-only {where.Split('.')[0]}.{k}"] = KeyStats.GetValueOrDefault($"code-only {where.Split('.')[0]}.{k}") + 1;
                    foreach (var k in extra) KeyStats[$"file-only {where.Split('.')[0]}.{k}"] = KeyStats.GetValueOrDefault($"file-only {where.Split('.')[0]}.{k}") + 1;
                    if (missing.Count > 0 || extra.Count > 0)
                        staleDiff = new Diff("fields", $"code-only [{string.Join(",", missing.Take(3))}] file-only [{string.Join(",", extra.Take(3))}]", where, true);
                    var common = ek.Intersect(ak).ToList();
                    var orderInFile = ak.Where(common.Contains).ToList();
                    // A reorder in a map whose field SET also changed is the same staleness;
                    // only a pure reorder is a disagreement about declaration order.
                    if (!common.SequenceEqual(orderInFile))
                        return new Diff("order", $"code {string.Join(",", common.Take(4))}… file {string.Join(",", orderInFile.Take(4))}…", where, staleDiff != null);
                    foreach (var k in common)
                    {
                        var d = Compare(em[k], am[k], where + "." + k);
                        if (d != null && !d.Stale) return d;
                        staleDiff ??= d;
                    }
                    return staleDiff;
                }
                case YSeq es when act is YSeq aseq:
                    if (es.List.Count > 0 && aseq.List.Count > 0) return Compare(es.List[0], aseq.List[0], where + "[0]");
                    return null;
                case YScalar when act is YScalar:
                    return null;
                // An empty list may be written either way; an empty hex blob is an empty scalar.
                case YSeq { List.Count: 0 } when act is YScalar { Value.Length: 0 }:
                case YScalar { Value.Length: 0 } when act is YSeq { List.Count: 0 }:
                    return null;
                default:
                    return new Diff("kind", $"code {Kind(exp)} vs file {Kind(act)}", where, false);
            }
        }

        static string Kind(YNode n) => n switch
        {
            YMap m => m.Flow ? "flow-map" : "map",
            YSeq q => "seq",
            _ => "scalar",
        };
    }
}
