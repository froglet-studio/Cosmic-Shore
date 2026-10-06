using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;

namespace CosmicShore.AssetTool
{
    /// <summary>
    /// cs-asset serialization-audit [path...]: for every script component and ScriptableObject
    /// Unity wrote under Assets/, compare each YAML key with (a) what Unity's serializer reads
    /// (<see cref="UnitySerializationRules"/>) and (b) what Prisma's loader assigns
    /// (<see cref="SerializedReader"/>), recursing into nested [Serializable] classes and lists.
    ///
    ///   DROPPED  Unity reads it, Prisma does not   - the value silently never arrives (worst)
    ///   EXTRA    Prisma reads it, Unity does not   - Prisma loads data Unity ignores
    ///   STALE    neither reads it                  - left over from an older script; harmless
    ///   MANAGED  a [SerializeReference] block      - Prisma does not load managed references yet
    ///
    /// Exit code 1 when anything is DROPPED or EXTRA, so it can gate a build.
    /// </summary>
    static class SerializationAudit
    {
        sealed class Tally { public int Count; public string Example = ""; }

        public static int Run(List<string> paths, bool json)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".unity", ".prefab", ".asset" };
            if (paths.Count == 0) paths.Add(Scripts.Db.AssetsRoot);
            var files = paths.SelectMany(p => Directory.Exists(p)
                ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories).Where(f => exts.Contains(Path.GetExtension(f)))
                : new[] { p }).ToList();

            var classes = new Dictionary<string, Dictionary<string, Tally>>
            {
                ["DROPPED"] = new(), ["EXTRA"] = new(), ["STALE"] = new(), ["MANAGED"] = new(),
            };
            int instances = 0, keys = 0, unresolved = 0, standIns = 0;
            foreach (var f in files)
            {
                string text;
                try { text = File.ReadAllText(f); } catch (IOException) { continue; }
                if (!text.StartsWith("%YAML", StringComparison.Ordinal)) continue;
                var rel = Path.GetRelativePath(Scripts.Db.ProjectRoot, f);
                foreach (var d in UnityYaml.ParseDocuments(text))
                {
                    if (d.ClassId != 114 || d.Stripped) continue;
                    string guid = d.Body["m_Script"]?.Str("guid");
                    if (string.IsNullOrEmpty(guid)) continue;
                    var type = Scripts.Types.Resolve(guid);
                    if (type == null) { unresolved++; continue; }
                    if (Scripts.Catalog.FromGuid(guid).IsStandIn || !UnitySerializationRules.IsScriptType(type)) { standIns++; continue; }
                    instances++;
                    Walk(type, d.Body, type.Name, rel, classes, ref keys, top: true);
                }
            }

            int dropped = classes["DROPPED"].Values.Sum(t => t.Count), extra = classes["EXTRA"].Values.Sum(t => t.Count);
            if (json)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    instances, keys, unresolved, standIns,
                    classes = classes.ToDictionary(c => c.Key, c => c.Value.OrderByDescending(kv => kv.Value.Count)
                        .Select(kv => new { key = kv.Key, count = kv.Value.Count, example = kv.Value.Example })),
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"{instances} script instances, {keys} keys checked; {unresolved} unresolvable scripts and {standIns} package/built-in instances skipped");
                foreach (var (name, what) in new[]
                {
                    ("DROPPED", "Unity reads, Prisma does not (values that never arrive)"),
                    ("EXTRA", "Prisma reads, Unity ignores"),
                    ("MANAGED", "[SerializeReference] blocks Prisma does not load"),
                    ("STALE", "neither reads (left over from older scripts)"),
                })
                {
                    var c = classes[name];
                    Console.WriteLine($"\n{name}: {c.Values.Sum(t => t.Count)} key(s) over {c.Count} field(s) - {what}");
                    foreach (var kv in c.OrderByDescending(kv => kv.Value.Count).Take(name == "STALE" ? 15 : 60))
                        Console.WriteLine($"  {kv.Value.Count,6}  {kv.Key}   e.g. {kv.Value.Example}");
                }
            }
            return dropped + extra > 0 ? 1 : 0;
        }

        static void Note(Dictionary<string, Dictionary<string, Tally>> classes, string cls, string key, string example)
        {
            var c = classes[cls];
            if (!c.TryGetValue(key, out var t)) c[key] = t = new Tally { Example = example };
            t.Count++;
        }

        static void Walk(Type type, YMap body, string path, string file,
                         Dictionary<string, Dictionary<string, Tally>> classes, ref int keys, bool top)
        {
            foreach (var entry in body.Entries)
            {
                var key = entry.Key;
                if (top && SerializedReader.IsBookkeeping(key)) continue;
                if (top && key == "references") { Note(classes, "MANAGED", $"{path}.references", file); continue; }
                keys++;
                var field = UnitySerializationRules.UnityField(type, key, out bool builtIn);
                if (builtIn) continue;   // a built-in base's native key: not judged here
                bool prisma = SerializedReader.Accepts(type, key);
                string where = $"{path}.{key}";
                if (field != null && !prisma) { Note(classes, "DROPPED", where, file); continue; }
                if (field == null && prisma) { Note(classes, "EXTRA", where, file); continue; }
                if (field == null) { Note(classes, "STALE", where, file); continue; }

                // Read by both: follow nested [Serializable] classes and lists of them.
                var ft = field.FieldType;
                Type elem = ft.IsArray ? ft.GetElementType() : ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>) ? ft.GetGenericArguments()[0] : null;
                if (elem == null && entry.Value is YMap map && Nested(ft))
                    Walk(ft, map, $"{path}.{key}", file, classes, ref keys, top: false);
                else if (elem != null && Nested(elem) && entry.Value is YSeq seq)
                    foreach (var item in seq.Items)
                        if (item is YMap im) Walk(elem, im, $"{path}.{key}[]", file, classes, ref keys, top: false);
            }
        }

        /// <summary>A nested serializable class or struct of script code (not a reference to a Unity object).</summary>
        static bool Nested(Type t) =>
            !t.IsPrimitive && !t.IsEnum && t != typeof(string) && !typeof(CosmicShore.Engine.Object).IsAssignableFrom(t)
            && !t.IsInterface && !t.IsAbstract && UnitySerializationRules.IsScriptType(t);
    }
}
