using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CosmicShore.Content;
using CosmicShore.Content.Models;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;

namespace CosmicShore.AssetTool
{
    /// <summary>
    /// What Prisma's editor pages read (TOOLS, DATA, MODELS), as JSON on stdout. The launcher runs
    /// these from the branch's own build, so the pages always describe the checkout they show.
    ///
    ///   cs-asset tools                       every FrogletTools menu item, with its metadata and source
    ///   cs-asset datasets                    every ScriptableObject data file, grouped by script type
    ///   cs-asset dataset &lt;file.asset&gt;        one data file's fields: values, types, headers, tooltips, ranges
    ///   cs-asset model &lt;file.fbx&gt;            one model: nodes, meshes, materials, blend shapes, bones, clips, settings
    ///   cs-asset model-preview &lt;file.fbx&gt; --out x.png [--size 512] [--yaw 145] [--pitch 20]
    /// </summary>
    public static class EditorData
    {
        static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            // Plain ASCII on stdout (every other character as \uXXXX): on Windows a redirected console
            // writes in the OEM code page, where "↔" becomes byte 0x1D and the reader's JSON breaks.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.BasicLatin),
        };

        static int Write(object o) { Console.WriteLine(JsonSerializer.Serialize(o, Json)); return 0; }

        static string Rel(string full) => Path.GetRelativePath(Scripts.Db.ProjectRoot, full).Replace('\\', '/');

        static string Full(string path)
        {
            var full = Path.IsPathRooted(path) ? path : Path.Combine(Scripts.Db.ProjectRoot, path);
            if (!File.Exists(full)) full = Path.GetFullPath(path);
            if (!File.Exists(full)) throw new ArgumentException($"no file '{path}'");
            return full;
        }

        // ================================================================ FrogletTools

        public sealed record Tool(string Menu, string Name, string Group, string Category, int Importance, bool Annotated,
            string Description, string Doc, string File, int Line, string Class, string Method, string Kind, string Writes, string Summary);

        // Positional or named arguments after the path: ("X", true) is a validator; the priority is the last integer.
        static readonly Regex MenuRx = new(@"\[MenuItem\(\s*""(FrogletTools/[^""]+)""((?:[^()\]]|\([^()]*\))*)\)\]", RegexOptions.Compiled);
        static readonly Regex MethodRx = new(@"\bstatic\s+[\w<>\[\],\s]*?\b(\w+)\s*\(", RegexOptions.Compiled);
        static readonly Regex ClassRx = new(@"\b(?:class|struct)\s+(\w+)", RegexOptions.Compiled);

        /// <summary>cs-asset tools: every [MenuItem("FrogletTools/...")] under Assets/, read from source the way FrogletToolRegistry reads it by reflection.</summary>
        public static int Tools() => Write(new { tools = ScanTools(Scripts.Db.AssetsRoot) });

        public static List<Tool> ScanTools(string assetsRoot)
        {
            var list = new List<Tool>();
            var root = Directory.GetParent(assetsRoot)!.FullName;
            foreach (var file in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                if (!text.Contains("FrogletTools/", StringComparison.Ordinal)) continue;
                bool writer = text.Contains("FrogletToolShipPanel", StringComparison.Ordinal) || text.Contains("FrogletToolChangeLedger.Record", StringComparison.Ordinal);
                foreach (Match m in MenuRx.Matches(text))
                {
                    var args = m.Groups[2].Value;
                    if (Regex.IsMatch(args, @"^\s*,\s*(?:isValidateFunction\s*:\s*)?true\b")) continue; // a menu validator, not a tool
                    var prio = Regex.Matches(args, @"-?\d+").Cast<Match>().LastOrDefault();
                    var menu = StripHotkey(m.Groups[1].Value);
                    // The attribute run this MenuItem sits in: back to the previous member's end, on to the method.
                    int start = Math.Max(text.LastIndexOf('}', m.Index), text.LastIndexOf(';', m.Index)) + 1;
                    var mm = MethodRx.Match(text, m.Index + m.Length);
                    int end = mm.Success ? mm.Index : Math.Min(text.Length, m.Index + 600);
                    var attrs = text[start..end];
                    var cls = ClassRx.Matches(text[..m.Index]).Cast<Match>().LastOrDefault()?.Groups[1].Value ?? "";
                    var segs = menu.Split('/');
                    string group = segs.Length >= 3 ? string.Join("/", segs.Skip(1).Take(segs.Length - 2)) : null;
                    int fi = attrs.IndexOf("[FrogletTool(", StringComparison.Ordinal);
                    string fa = fi >= 0 ? attrs[fi..] : "";
                    string category = Regex.Match(fa, @"FrogletToolCategory\.(\w+)") is { Success: true } c ? c.Groups[1].Value : InferCategory(menu, cls);
                    int importance = Regex.Match(fa, @"Importance\s*=\s*(\d)") is { Success: true } im ? int.Parse(im.Groups[1].Value)
                        : InferImportance(prio != null ? int.Parse(prio.Value) : 1000);
                    string displayName = StringArg(fa, "DisplayName");
                    string summary = ClassSummary(text, cls);
                    string writes = writer ? "writer"
                        : summary != null && Regex.IsMatch(summary, @"\bREADER\b|reader only|read-only|writes no", RegexOptions.IgnoreCase) ? "reader" : "unknown";
                    bool window = Regex.IsMatch(text, $@"class\s+{Regex.Escape(cls)}\s*:\s*[\w.]*EditorWindow") ||
                                  (mm.Success && Regex.IsMatch(text.Substring(mm.Index, Math.Min(400, text.Length - mm.Index)), @"GetWindow"));
                    list.Add(new Tool(menu, displayName ?? segs[^1], group, category, Math.Clamp(importance, 1, 5), fi >= 0,
                        StringArg(fa, "Description"), StringArg(fa, "DocPath"),
                        Path.GetRelativePath(root, file).Replace('\\', '/'), 1 + text.AsSpan(0, m.Index).Count('\n'),
                        cls, mm.Success ? mm.Groups[1].Value : null, window ? "window" : "action", writes, summary));
                }
            }
            return list.GroupBy(t => t.Menu).Select(g => g.First())
                .OrderBy(t => t.Category, StringComparer.Ordinal).ThenByDescending(t => t.Importance).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>A hotkey suffix is one last token starting with %, #, &amp; or _ (" %&amp;m", " _g"); "Warnings &amp; Errors" is a name.</summary>
        static string StripHotkey(string path) => Regex.Replace(path, @"\s[%#&_]\S+$", "");

        /// <summary>A named string argument of an attribute: concatenated C# literals ("a" + "b", @"c") joined.</summary>
        static string StringArg(string attr, string name)
        {
            var m = Regex.Match(attr, name + @"\s*=\s*");
            if (!m.Success) return null;
            var sb = new StringBuilder();
            int i = m.Index + m.Length;
            while (i < attr.Length)
            {
                while (i < attr.Length && char.IsWhiteSpace(attr[i])) i++;
                bool verbatim = i < attr.Length && attr[i] == '@';
                if (verbatim) i++;
                if (i >= attr.Length || attr[i] != '"') break;
                i++;
                while (i < attr.Length)
                {
                    char ch = attr[i];
                    if (verbatim && ch == '"' && i + 1 < attr.Length && attr[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                    if (!verbatim && ch == '\\' && i + 1 < attr.Length)
                    {
                        char e = attr[i + 1];
                        sb.Append(e switch { 'n' => '\n', 't' => '\t', _ => e });
                        i += 2; continue;
                    }
                    if (ch == '"') { i++; break; }
                    sb.Append(ch); i++;
                }
                while (i < attr.Length && char.IsWhiteSpace(attr[i])) i++;
                if (i < attr.Length && attr[i] == '+') { i++; continue; }
                break;
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>The first paragraph of a class's /// summary, tags stripped.</summary>
        static string ClassSummary(string text, string cls)
        {
            if (string.IsNullOrEmpty(cls)) return null;
            var cm = Regex.Match(text, $@"\b(?:class|struct)\s+{Regex.Escape(cls)}\b");
            if (!cm.Success) return null;
            var before = text[..cm.Index];
            int s = before.LastIndexOf("<summary>", StringComparison.Ordinal), e = before.LastIndexOf("</summary>", StringComparison.Ordinal);
            if (s < 0 || e < s || cm.Index - e > 400) return null;
            var body = string.Join(" ", before[(s + 9)..e].Split('\n').Select(l => l.Trim().TrimStart('/').Trim()));
            body = Regex.Replace(body, @"<see cref=""([^""]+)""\s*/>", "$1");
            body = Regex.Replace(body, @"<[^>]+>", "");
            body = Regex.Replace(body, @"\s+", " ").Trim();
            return body.Length > 600 ? body[..597] + "..." : body;
        }

        // The registry's own rules (FrogletToolRegistry.InferCategory / InferImportance) for tools without the attribute.
        static string InferCategory(string path, string cls)
        {
            var hay = (path + " " + cls).ToLowerInvariant();
            bool Has(params string[] n) => n.Any(x => hay.Contains(x, StringComparison.Ordinal));
            if (Has("build", "release", "player")) return "Build";
            if (Has("benchmark", "performance", "memory", "profil", "texture")) return "Performance";
            if (Has("crash", "diagnos", "watchdog", "bug ledger", "bugledger")) return "Diagnostics";
            if (Has("audit", "validate", "validator", "verify", "integrity")) return "Validation";
            if (Has("prism", "cell", "lifeform", "crystal", "ecolog", "flora", "fauna", "toybox")) return "Ecology";
            if (Has("vessel", "ship", "hud", "elemental", "petal", "rig", "ability")) return "Vessels";
            if (Has("game mode", "gamemode", "minigame", "end game", "endcondition", "end condition", "arcade")) return "GameModes";
            if (Has("canvas", "toast", "dialogue", "ui", "raycast", "notification")) return "Interface";
            if (Has("ugs", "product", "leaderboard", "cloud")) return "Services";
            if (Has("scene", "setup", "spawn", "photobooth", "recording")) return "SceneSetup";
            return "Misc";
        }

        static int InferImportance(int priority) => priority is <= 0 or >= 1000 ? 3 : priority < 50 ? 5 : priority < 150 ? 4 : priority < 400 ? 3 : 2;

        // ================================================================ data sets

        static readonly Regex ScriptGuidRx = new(@"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);
        static readonly Regex NameRx = new(@"^\s*m_Name:\s*(.*)$", RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>cs-asset datasets: every ScriptableObject .asset in the project, grouped by its script.</summary>
        public static int Datasets()
        {
            var groups = new Dictionary<string, (string type, string ns, string script, List<object> items)>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(Scripts.Db.AssetsRoot, "*.asset", SearchOption.AllDirectories))
            {
                string head;
                using (var r = new StreamReader(file)) { var buf = new char[4096]; int n = r.Read(buf, 0, buf.Length); head = new string(buf, 0, n); }
                if (!head.StartsWith("%YAML", StringComparison.Ordinal) || !head.Contains("MonoBehaviour:", StringComparison.Ordinal)) continue;
                var gm = ScriptGuidRx.Match(head);
                if (!gm.Success) continue;
                var guid = gm.Groups[1].Value;
                if (!groups.TryGetValue(guid, out var g))
                {
                    var info = Scripts.Catalog.FromGuid(guid);
                    string type = info.Type?.Name ?? (info.Path != null ? Path.GetFileNameWithoutExtension(info.Path) : "(missing script " + guid[..8] + ")");
                    g = (type, info.Type?.Namespace, info.Path != null ? Rel(info.Path) : null, new List<object>());
                    groups[guid] = g;
                }
                var nm = NameRx.Match(head);
                g.items.Add(new { path = Rel(file), name = nm.Success ? nm.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(file) });
            }
            var types = groups.Select(kv => new { guid = kv.Key, kv.Value.type, kv.Value.ns, kv.Value.script, count = kv.Value.items.Count, items = kv.Value.items })
                .OrderByDescending(t => t.count).ThenBy(t => t.type, StringComparer.OrdinalIgnoreCase).ToList();
            return Write(new { files = types.Sum(t => t.count), types });
        }

        public sealed class FieldInfoJson
        {
            public string Key { get; set; }
            public string Label { get; set; }
            public string Path { get; set; }
            public string Kind { get; set; }
            public string Type { get; set; }
            public string Value { get; set; }
            public bool Editable { get; set; }
            public string Header { get; set; }
            public string Tooltip { get; set; }
            public float[] Range { get; set; }
            public bool Multiline { get; set; }
            public List<string[]> Options { get; set; }
            public string RefPath { get; set; }
            public bool Stale { get; set; }
            public int Count { get; set; }
            public List<FieldInfoJson> Children { get; set; }
        }

        /// <summary>
        /// cs-asset dataset FILE: each ScriptableObject in the file with its fields in file order -
        /// the value as YAML has it, what kind of value it is (number, toggle, text, enum, vector,
        /// colour, reference, list, object), and the script's [Header]/[Tooltip]/[Range]. Simple
        /// values are marked editable; `cs-asset set FILE &amp;ID PATH VALUE` writes one.
        /// </summary>
        public static int Dataset(string path)
        {
            var full = Full(path);
            var docs = UnityYaml.ParseDocuments(File.ReadAllText(full));
            var objects = new List<object>();
            foreach (var d in docs.Where(d => d.ClassId == 114 && d.Body != null))
            {
                var body = d.Body;
                string guid = ObjRef.From(body["m_Script"]).Guid;
                var info = guid != null ? Scripts.Catalog.FromGuid(guid) : null;
                var type = info?.Type;
                var fields = new List<FieldInfoJson>();
                string header = null;
                foreach (var kv in body.Entries)
                {
                    if (kv.Key.StartsWith("m_", StringComparison.Ordinal) && kv.Key is "m_ObjectHideFlags" or "m_CorrespondingSourceObject" or "m_PrefabInstance"
                        or "m_PrefabAsset" or "m_GameObject" or "m_Enabled" or "m_EditorHideFlags" or "m_Script" or "m_Name" or "m_EditorClassIdentifier") continue;
                    FieldInfo fi = type != null ? UnitySerializationRules.UnityField(type, kv.Key, out _) : null;
                    var f = Describe(kv.Key, kv.Key, kv.Value, fi?.FieldType, fi, 0);
                    f.Stale = type != null && fi == null;
                    var h = fi?.GetCustomAttributes(true).FirstOrDefault(a => a.GetType().Name == "HeaderAttribute");
                    if (h != null) header = (string)h.GetType().GetField("header")!.GetValue(h)!;
                    f.Header = h != null ? header : null;
                    fields.Add(f);
                }
                objects.Add(new
                {
                    fileId = d.FileId,
                    name = body.Str("m_Name"),
                    type = type?.Name ?? (info?.Path != null ? Path.GetFileNameWithoutExtension(info.Path) : null),
                    ns = type?.Namespace,
                    script = info?.Path != null ? Rel(info.Path) : null,
                    resolved = type != null,
                    fields,
                });
            }
            return Write(new { path = Rel(full), objects });
        }

        static readonly HashSet<string> Vec = new() { "x,y", "x,y,z", "x,y,z,w" };

        static FieldInfoJson Describe(string key, string path, YNode node, Type t, FieldInfo fi, int depth)
        {
            var f = new FieldInfoJson { Key = key, Path = path, Label = Nicify(key), Type = t != null ? TypeName(t) : null };
            if (fi != null)
            {
                foreach (var a in fi.GetCustomAttributes(true))
                    switch (a.GetType().Name)
                    {
                        case "TooltipAttribute": f.Tooltip = (string)a.GetType().GetField("tooltip")!.GetValue(a); break;
                        case "RangeAttribute": f.Range = new[] { (float)a.GetType().GetField("min")!.GetValue(a)!, (float)a.GetType().GetField("max")!.GetValue(a)! }; break;
                        case "TextAreaAttribute": case "MultilineAttribute": f.Multiline = true; break;
                    }
            }
            switch (node)
            {
                case YScalar s:
                    f.Value = s.Value;
                    if (t != null && t.IsEnum)
                    {
                        f.Kind = "enum";
                        f.Options = Enum.GetValues(t).Cast<object>().Select(v => new[] { Enum.GetName(t, v)!, Convert.ToInt64(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) }).ToList();
                    }
                    else if (t == typeof(bool)) f.Kind = "bool";
                    else if (t == typeof(string)) f.Kind = "text";
                    else if (t != null && (t.IsPrimitive || t == typeof(decimal))) f.Kind = "number";
                    else f.Kind = double.TryParse(s.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? "number" : "text";
                    f.Editable = true;
                    break;
                case YMap m when m.Has("fileID"):
                {
                    var r = ObjRef.From(m);
                    f.Kind = "ref";
                    f.Value = r.IsNull ? "None" : r.ToString();
                    if (!r.IsNull && !r.IsLocal) f.RefPath = Scripts.Db.PathOf(r.Guid) is { } p ? Rel(p) : "(missing " + r.Guid[..8] + ")";
                    else if (!r.IsNull) f.RefPath = $"&{r.FileId} (this file)";
                    break;
                }
                case YMap m when Vec.Contains(string.Join(",", m.Entries.Select(e => e.Key))) || string.Join(",", m.Entries.Select(e => e.Key)) == "r,g,b,a":
                    f.Kind = m.Has("r") ? "color" : "vector";
                    f.Value = "{" + string.Join(", ", m.Entries.Select(e => e.Key + ": " + e.Value.Scalar)) + "}";
                    f.Editable = m.Entries.All(e => e.Value is YScalar);
                    break;
                case YMap m:
                    f.Kind = "object";
                    f.Count = m.Entries.Count;
                    if (depth < 4)
                        f.Children = m.Entries.Select(e =>
                        {
                            FieldInfo cf = t != null ? UnitySerializationRules.UnityField(t, e.Key, out _) : null;
                            return Describe(e.Key, path + "." + e.Key, e.Value, cf?.FieldType, cf, depth + 1);
                        }).ToList();
                    break;
                case YSeq q:
                {
                    f.Kind = "list";
                    f.Count = q.List.Count;
                    var et = t == null ? null : t.IsArray ? t.GetElementType() : t.IsGenericType ? t.GetGenericArguments()[0] : null;
                    if (depth < 4)
                        f.Children = q.List.Take(200).Select((e, i) => Describe($"[{i}]", $"{path}[{i}]", e, et, null, depth + 1)).ToList();
                    break;
                }
                default:
                    f.Kind = "text"; f.Value = ""; f.Editable = true; break;
            }
            return f;
        }

        static string TypeName(Type t) => t.IsGenericType
            ? t.Name[..t.Name.IndexOf('`')] + "<" + string.Join(", ", t.GetGenericArguments().Select(TypeName)) + ">"
            : t.IsArray ? TypeName(t.GetElementType()!) + "[]" : t.Name;

        /// <summary>Unity's inspector label: "m_maxSpeed" / "_maxSpeed" / "&lt;MaxSpeed&gt;k__BackingField" -> "Max Speed".</summary>
        public static string Nicify(string key)
        {
            if (key.StartsWith("<", StringComparison.Ordinal) && key.Contains(">k__BackingField", StringComparison.Ordinal)) key = key[1..key.IndexOf('>')];
            if (key.StartsWith("m_", StringComparison.Ordinal)) key = key[2..];
            key = key.TrimStart('_');
            if (key.Length > 1 && key[0] == 'k' && char.IsUpper(key[1])) key = key[1..];
            var sb = new StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (i == 0) { sb.Append(char.ToUpperInvariant(c)); continue; }
                bool split = char.IsUpper(c) && (!char.IsUpper(key[i - 1]) || (i + 1 < key.Length && char.IsLower(key[i + 1])))
                             || char.IsDigit(c) && !char.IsDigit(key[i - 1]);
                if (split) sb.Append(' ');
                sb.Append(c == '_' ? ' ' : c);
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        // ================================================================ models

        static ImportedModel LoadModel(string full)
        {
            var guid = Scripts.Db.GuidOf(full);
            var settings = ModelImportSettings.FromMeta(File.Exists(full + ".meta") ? UnityYaml.ParseDocuments(File.ReadAllText(full + ".meta")).FirstOrDefault()?.Body : null);
            // Blender/Maya: through the installed application, as Unity imports them; say why when it cannot.
            if (DccModelConverter.IsDccPath(full))
                return DccModelConverter.Import(full, settings, guid, out var error) ?? throw new ArgumentException(error);
            var model = guid != null ? Scripts.Db.LoadModel(guid) : null;
            return model ?? FbxModelImporter.Import(full, settings);
        }

        /// <summary>cs-asset model FILE: what Unity's importer makes of an FBX, as the game sees it.</summary>
        public static int Model(string path)
        {
            var full = Full(path);
            var model = LoadModel(full) ?? throw new ArgumentException("the model did not import");
            var (lo, hi) = Bounds(model);
            var meshes = model.Meshes.Where(m => m.Mesh != null).Select(m => new
            {
                name = m.Name,
                node = m.Node?.Path,
                vertices = m.Mesh.vertexCount,
                triangles = Enumerable.Range(0, m.Mesh.subMeshCount).Sum(i => m.Mesh.GetTriangles(i).Length / 3),
                submeshes = m.Mesh.subMeshCount,
                materials = m.Node?.Materials.ToList() ?? new List<string>(),
                blendShapes = Enumerable.Range(0, m.Mesh.blendShapeCount).Select(m.Mesh.GetBlendShapeName).ToList(),
                skinned = m.Node?.Skinned ?? false,
                bones = m.Node?.Bones.Count ?? 0,
                uvSets = Enumerable.Range(0, 8).Count(c => HasUv(m.Mesh, c)),
                hasColors = m.Mesh.colors is { Length: > 0 },
            }).ToList();
            var clips = model.Scene.ObjectList.Where(o => o.Kind == "AnimationStack").Select(o => o.Name).ToList();
            var s = model.Settings;
            var uses = ModelMaterialUsage.Find(Scripts.Db, model);
            var resolved = ModelMaterialUsage.Resolve(model, uses, out var fromPrefab);
            var gameMaterials = model.Meshes.Where(m => m.Mesh != null).Select(m => new
            {
                mesh = m.Name,
                materials = (resolved.TryGetValue(m.FileId, out var refs) ? refs : new List<ObjRef>()).Select(r =>
                {
                    var sw = ModelMaterialUsage.Swatch(Scripts.Db, model, r);
                    return new { name = sw.Name, path = sw.Path is { } mp ? Rel(mp) : null, shader = sw.Shader, color = Hex(sw.Color) };
                }).ToList(),
            }).ToList();
            return Write(new
            {
                path = Rel(full),
                guid = model.Guid,
                sizeKB = new FileInfo(full).Length / 1024,
                nodes = model.Nodes.Count,
                meshCount = meshes.Count,
                vertices = meshes.Sum(m => m.vertices),
                triangles = meshes.Sum(m => m.triangles),
                materials = meshes.SelectMany(m => m.materials).Where(n => n != null).Distinct().ToList(),
                convertedBy = DccModelConverter.ToolFor(full),
                blendShapes = meshes.Sum(m => m.blendShapes.Count),
                skinned = meshes.Any(m => m.skinned),
                bounds = new { min = new[] { lo.X, lo.Y, lo.Z }, max = new[] { hi.X, hi.Y, hi.Z }, size = new[] { hi.X - lo.X, hi.Y - lo.Y, hi.Z - lo.Z } },
                unitScale = model.UnitScale,
                fileScale = model.FileScale,
                settings = new
                {
                    s.GlobalScale, s.UseFileScale, s.BakeAxisConversion, s.ImportBlendShapes, s.WeldVertices, s.PreserveHierarchy,
                    s.ImportAnimation, clips = s.Clips.Select(c => new { c.Name, c.TakeName, c.FirstFrame, c.LastFrame, c.LoopTime }).ToList(),
                    externalMaterials = s.ExternalMaterials.ToDictionary(kv => kv.Key, kv => Scripts.Db.PathOf(kv.Value.Guid ?? "") is { } p ? Rel(p) : kv.Value.ToString()),
                },
                takes = clips,
                materialSource = fromPrefab is { } fp ? Rel(fp) : null,
                usedBy = uses.Select(u => Rel(u.Prefab)).Distinct().ToList(),
                gameMaterials,
                hierarchy = model.Nodes.Take(400).Select(n => n.Path).ToList(),
                meshes,
                warnings = model.Warnings.ToList(),
                discardedPolygons = model.DiscardedPolygons,
            });
        }

        static string Hex(CosmicShore.Engine.Color? c) => c is { } v
            ? $"#{(int)Math.Round(Math.Clamp(v.r, 0, 1) * 255):x2}{(int)Math.Round(Math.Clamp(v.g, 0, 1) * 255):x2}{(int)Math.Round(Math.Clamp(v.b, 0, 1) * 255):x2}"
            : null;

        /// <summary>The colour of each mesh's submesh in the materials the game gives it (<see cref="ModelMaterialUsage"/>).</summary>
        static Func<ImportedMesh, int, (double r, double g, double b)?> GameColors(ImportedModel model, out string prefab)
        {
            var resolved = ModelMaterialUsage.Resolve(model, ModelMaterialUsage.Find(Scripts.Db, model), out prefab);
            // Read every swatch up front: the turntable renders its views in parallel.
            var colors = resolved.Values.SelectMany(r => r).Distinct().ToDictionary(r => r,
                r => ModelMaterialUsage.Swatch(Scripts.Db, model, r).Color is { } c ? ((double, double, double)?)(c.r, c.g, c.b) : null);
            return (mesh, sub) =>
            {
                if (!resolved.TryGetValue(mesh.FileId, out var refs) || refs.Count == 0) return null;
                return colors[refs[Math.Min(sub, refs.Count - 1)]];
            };
        }

        static bool HasUv(CosmicShore.Engine.Mesh mesh, int channel)
        {
            try
            {
                var uvs = new List<CosmicShore.Engine.Vector2>();
                mesh.GetUVs(channel, uvs);
                return uvs.Count > 0;
            }
            catch { return false; }
        }

        /// <param name="posed">Vertices to use instead of a mesh's own (its blend shapes applied), or null.</param>
        static IEnumerable<(DVec3 a, DVec3 b, DVec3 c, int sub, ImportedMesh mesh)> Triangles(ImportedModel model, IReadOnlyDictionary<ImportedMesh, CosmicShore.Engine.Vector3[]> posed = null)
        {
            foreach (var im in model.Meshes)
            {
                var mesh = im.Mesh;
                if (mesh == null) continue;
                var mat = im.Node?.ModelMatrix ?? DMat4.Identity;
                var v = posed != null && posed.TryGetValue(im, out var pv) ? pv : mesh.vertices;
                var w = new DVec3[v.Length];
                for (int i = 0; i < v.Length; i++) w[i] = mat.Point(new DVec3(v[i].x, v[i].y, v[i].z));
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var t = mesh.GetTriangles(s);
                    for (int i = 0; i + 2 < t.Length; i += 3) yield return (w[t[i]], w[t[i + 1]], w[t[i + 2]], s, im);
                }
            }
        }

        static (DVec3 lo, DVec3 hi) Bounds(ImportedModel model, IReadOnlyDictionary<ImportedMesh, CosmicShore.Engine.Vector3[]> posed = null)
        {
            double[] lo = { double.MaxValue, double.MaxValue, double.MaxValue }, hi = { double.MinValue, double.MinValue, double.MinValue };
            foreach (var (a, b, c, _, _) in Triangles(model, posed))
                foreach (var p in new[] { a, b, c })
                    for (int k = 0; k < 3; k++) { lo[k] = Math.Min(lo[k], p[k]); hi[k] = Math.Max(hi[k], p[k]); }
            if (lo[0] > hi[0]) return (default, default);
            return (new DVec3(lo[0], lo[1], lo[2]), new DVec3(hi[0], hi[1], hi[2]));
        }

        /// <summary>
        /// cs-asset model-preview FILE --out PNG: a shaded picture of the model, drawn on the CPU
        /// (no GPU, works headless): every mesh at its node's pose, a colour per submesh, a key and
        /// a rim light, 2x supersampled, framed to fill the picture. Yaw turns the camera around the
        /// model (0 looks at its back, 180 at its front - Unity's forward is +Z; 145 is a front
        /// three-quarter view), pitch raises it.
        /// </summary>
        public static int ModelPreview(string path, Dictionary<string, string> opts)
        {
            var full = Full(path);
            var outPath = opts.TryGetValue("out", out var o) ? o : throw new ArgumentException("--out <file.png> is needed");
            int size = Math.Clamp(opts.TryGetValue("size", out var sz) ? int.Parse(sz, CultureInfo.InvariantCulture) : 512, 32, 2048);
            double yaw = opts.TryGetValue("yaw", out var y) ? double.Parse(y, CultureInfo.InvariantCulture) : 145;
            double pitch = opts.TryGetValue("pitch", out var p) ? double.Parse(p, CultureInfo.InvariantCulture) : 20;
            var model = LoadModel(full) ?? throw new ArgumentException("the model did not import");
            // --colors game (default): each submesh in the colour of the material the game gives it;
            // --colors submesh: one key colour per submesh, to tell them apart.
            bool game = !opts.TryGetValue("colors", out var cm) || cm != "submesh";
            var colorOf = game ? GameColors(model, out _) : null;
            // --turntable N: N views around the model in one sheet (6 to a row) for drag-to-turn.
            int frames = opts.TryGetValue("turntable", out var tt) ? Math.Clamp(int.Parse(tt, CultureInfo.InvariantCulture), 2, 72) : 0;
            int cols = Math.Min(frames, 6);
            // --shapes "Name=50;Other=100": blend-shape weights (0-100, as a SkinnedMeshRenderer takes them).
            var weights = opts.TryGetValue("shapes", out var sw) ? ParseShapes(sw) : new Dictionary<string, float>();
            var posed = weights.Count > 0 ? Posed(model, weights) : null;
            var png = frames > 0 ? RenderTurntable(model, size, frames, cols, yaw, pitch, colorOf, posed) : Render(model, size, yaw, pitch, colorOf, posed);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            File.WriteAllBytes(outPath, png);
            return Write(new { path = Rel(full), @out = Path.GetFullPath(outPath), size, colors = colorOf != null ? "game" : "submesh",
                               frames = Math.Max(1, frames), cols = Math.Max(1, cols), yaw, step = frames > 0 ? 360.0 / frames : 0,
                               shapes = weights });
        }

        static Dictionary<string, float> ParseShapes(string spec)
        {
            var d = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var part in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.LastIndexOf('=');
                if (eq <= 0 || !float.TryParse(part[(eq + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var w))
                    throw new ArgumentException($"--shapes: '{part}' is not NAME=WEIGHT");
                d[part[..eq].Trim()] = w;
            }
            return d;
        }

        /// <summary>
        /// Each mesh's vertices with the named blend shapes at their weights (0-100), as the engine's
        /// skinning applies them: a shape's frames are its key weights, and a weight between two keys
        /// blends their deltas (below the first key it scales the first delta). Unknown names are ignored.
        /// </summary>
        public static Dictionary<ImportedMesh, CosmicShore.Engine.Vector3[]> Posed(ImportedModel model, IReadOnlyDictionary<string, float> weights)
        {
            var result = new Dictionary<ImportedMesh, CosmicShore.Engine.Vector3[]>();
            foreach (var im in model.Meshes)
            {
                var mesh = im.Mesh;
                if (mesh == null || mesh.blendShapeCount == 0) continue;
                var v = (CosmicShore.Engine.Vector3[])mesh.vertices.Clone();
                bool any = false;
                for (int sh = 0; sh < mesh.blendShapeCount; sh++)
                {
                    if (!weights.TryGetValue(mesh.GetBlendShapeName(sh), out var w) || w == 0) continue;
                    int frames = mesh.GetBlendShapeFrameCount(sh);
                    if (frames == 0) continue;
                    var d0 = new CosmicShore.Engine.Vector3[v.Length];
                    var d1 = new CosmicShore.Engine.Vector3[v.Length];
                    int hi = 0;
                    while (hi < frames - 1 && mesh.GetBlendShapeFrameWeight(sh, hi) < w) hi++;
                    float whi = mesh.GetBlendShapeFrameWeight(sh, hi);
                    mesh.GetBlendShapeFrameVertices(sh, hi, d1, null, null);
                    float k1, k0 = 0;
                    if (hi == 0) k1 = whi != 0 ? w / whi : 0;
                    else
                    {
                        float wlo = mesh.GetBlendShapeFrameWeight(sh, hi - 1);
                        mesh.GetBlendShapeFrameVertices(sh, hi - 1, d0, null, null);
                        float t = whi != wlo ? (w - wlo) / (whi - wlo) : 1;
                        k1 = t; k0 = 1 - t;
                    }
                    for (int i = 0; i < v.Length; i++) v[i] += d1[i] * k1 + d0[i] * k0;
                    any = true;
                }
                if (any) result[im] = v;
            }
            return result;
        }

        static readonly (double r, double g, double b)[] Palette =
        {
            (0.55, 0.80, 0.95), (0.95, 0.55, 0.80), (0.65, 0.95, 0.55), (0.98, 0.80, 0.45), (0.70, 0.60, 0.98), (0.55, 0.95, 0.90),
        };

        /// <param name="colorOf">The colour of a mesh's submesh (the game's material), or null for the key palette.</param>
        public static byte[] Render(ImportedModel model, int size, double yaw, double pitch,
                                    Func<ImportedMesh, int, (double r, double g, double b)?> colorOf = null, IReadOnlyDictionary<ImportedMesh, CosmicShore.Engine.Vector3[]> posed = null)
            => Png(RenderRgba(model, size, yaw, pitch, colorOf, sphereFit: false, posed), size, size);

        /// <summary>
        /// <paramref name="frames"/> views turning once around the model (frame k at yaw + k*360/frames),
        /// laid out <paramref name="cols"/> to a row, all framed alike (the model's bounding sphere)
        /// so the size holds still while it turns. Prisma's MODELS page drags through them.
        /// </summary>
        public static byte[] RenderTurntable(ImportedModel model, int size, int frames, int cols, double yaw, double pitch,
                                             Func<ImportedMesh, int, (double r, double g, double b)?> colorOf = null, IReadOnlyDictionary<ImportedMesh, CosmicShore.Engine.Vector3[]> posed = null)
        {
            int rows = (frames + cols - 1) / cols, w = size * cols, h = size * rows;
            var sheet = new byte[w * h * 4];
            System.Threading.Tasks.Parallel.For(0, frames, k =>
            {
                var cell = RenderRgba(model, size, (yaw + k * 360.0 / frames) % 360, pitch, colorOf, sphereFit: true, posed);
                int cx = k % cols * size, cy = k / cols * size;
                for (int y = 0; y < size; y++) Buffer.BlockCopy(cell, y * size * 4, sheet, ((cy + y) * w + cx) * 4, size * 4);
            });
            return Png(sheet, w, h);
        }

        static byte[] RenderRgba(ImportedModel model, int size, double yaw, double pitch,
                                 Func<ImportedMesh, int, (double r, double g, double b)?> colorOf, bool sphereFit, IReadOnlyDictionary<ImportedMesh, CosmicShore.Engine.Vector3[]> posed = null)
        {
            int ss = 2, n = size * ss;
            var (lo, hi) = Bounds(model, posed);
            var center = (lo + hi) * 0.5;
            double radius = Math.Max(1e-6, (hi - lo).Length * 0.5);
            // View: rotate the world so the camera looks down -Z from the front-right, above.
            var view = DMat4.RotX(pitch) * DMat4.RotY(-yaw) * DMat4.Translate(-center);
            // Fit what the camera actually sees: the model's extent on screen after turning it.
            double sx0 = double.MaxValue, sx1 = double.MinValue, sy0 = double.MaxValue, sy1 = double.MinValue;
            foreach (var (a0, b0, c0, _, _) in Triangles(model, posed))
                foreach (var q in new[] { view.Point(a0), view.Point(b0), view.Point(c0) })
                { sx0 = Math.Min(sx0, q.X); sx1 = Math.Max(sx1, q.X); sy0 = Math.Min(sy0, q.Y); sy1 = Math.Max(sy1, q.Y); }
            double span = Math.Max(Math.Max(sx1 - sx0, sy1 - sy0), radius * 1e-3);
            double scale = n * 0.86 / span;
            double offX = -(sx0 + sx1) * 0.5, offY = -(sy0 + sy1) * 0.5;
            if (sphereFit) { scale = n * 0.98 / (2 * radius); offX = offY = 0; }
            var color = new double[n * n * 3];
            var depth = new double[n * n];
            Array.Fill(depth, double.MaxValue);
            for (int yy = 0; yy < n; yy++)
                for (int xx = 0; xx < n; xx++)
                {
                    double t = (double)yy / n; int i = (yy * n + xx) * 3;
                    if (colorOf == null) { color[i] = 0.035 + 0.03 * t; color[i + 1] = 0.03 + 0.035 * t; color[i + 2] = 0.10 + 0.07 * t; }
                    else { color[i] = color[i + 1] = 0.30 - 0.12 * t; color[i + 2] = 0.33 - 0.12 * t; } // grey: dark hulls stay visible
                }
            var key = new DVec3(-0.45, 0.65, -0.6).Normalized;   // from the camera's upper left
            var rim = new DVec3(0.6, 0.2, 0.75).Normalized;      // from behind
            foreach (var (a0, b0, c0, sub, im) in Triangles(model, posed))
            {
                var a = view.Point(a0); var b = view.Point(b0); var c = view.Point(c0);
                var nrm = DVec3.Cross(b - a, c - a).Normalized;
                if (nrm.Length < 0.5) continue;
                if (nrm.Z > 0) nrm = -nrm; // two-sided: face the camera (camera looks along +Z here)
                double diffuse = Math.Max(0, DVec3.Dot(nrm, -key)), back = Math.Pow(Math.Max(0, DVec3.Dot(nrm, -rim)), 2);
                var pc = colorOf?.Invoke(im, sub) ?? Palette[sub % Palette.Length];
                double li = 0.22 + 0.78 * diffuse;
                double r = pc.r * li + 0.35 * back, g = pc.g * li + 0.45 * back, bl = pc.b * li + 0.6 * back;
                // screen: x right, y down; depth = view Z (bigger is farther)
                (double x, double y, double z) S(DVec3 v) => (n * 0.5 + (v.X + offX) * scale, n * 0.5 - (v.Y + offY) * scale, v.Z);
                var (ax, ay, az) = S(a); var (bx, by, bz) = S(b); var (cx, cy, cz) = S(c);
                int minX = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)))), maxX = Math.Min(n - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)))), maxY = Math.Min(n - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                double area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                if (Math.Abs(area) < 1e-12) continue;
                for (int py = minY; py <= maxY; py++)
                    for (int px = minX; px <= maxX; px++)
                    {
                        double qx = px + 0.5, qy = py + 0.5;
                        double w0 = ((bx - qx) * (cy - qy) - (by - qy) * (cx - qx)) / area;
                        double w1 = ((cx - qx) * (ay - qy) - (cy - qy) * (ax - qx)) / area;
                        double w2 = 1 - w0 - w1;
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                        double z = w0 * az + w1 * bz + w2 * cz;
                        int idx = py * n + px;
                        if (z >= depth[idx]) continue;
                        depth[idx] = z;
                        color[idx * 3] = r; color[idx * 3 + 1] = g; color[idx * 3 + 2] = bl;
                    }
            }
            // downsample with a soft outline where depth jumps
            var rgba = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double r = 0, g = 0, b = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            int i = ((y * ss + sy) * n + x * ss + sx) * 3;
                            r += color[i]; g += color[i + 1]; b += color[i + 2];
                        }
                    double k = 1.0 / (ss * ss);
                    int o2 = (y * size + x) * 4;
                    rgba[o2] = ToByte(r * k); rgba[o2 + 1] = ToByte(g * k); rgba[o2 + 2] = ToByte(b * k); rgba[o2 + 3] = 255;
                }
            return rgba;
        }

        static byte ToByte(double v) => (byte)Math.Clamp((int)Math.Round(Math.Pow(Math.Clamp(v, 0, 1), 1 / 1.1) * 255), 0, 255);

        /// <summary>A plain RGBA PNG (filter 0 per row, zlib-deflated).</summary>
        public static byte[] Png(byte[] rgba, int w, int h)
        {
            var raw = new byte[(w * 4 + 1) * h];
            for (int y = 0; y < h; y++) Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
            using var ms = new MemoryStream();
            ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            BE(ihdr, 0, (uint)w); BE(ihdr, 4, (uint)h); ihdr[8] = 8; ihdr[9] = 6;
            Chunk(ms, "IHDR", ihdr);
            using (var z = new MemoryStream())
            {
                using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) zs.Write(raw);
                Chunk(ms, "IDAT", z.ToArray());
            }
            Chunk(ms, "IEND", Array.Empty<byte>());
            return ms.ToArray();
        }

        static void BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; BE(len, 0, (uint)data.Length); s.Write(len);
            var td = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            s.Write(td);
            var crc = new byte[4]; BE(crc, 0, Crc(td)); s.Write(crc);
        }

        static uint Crc(byte[] d)
        {
            uint c = 0xFFFFFFFF;
            foreach (var x in d)
            {
                c ^= x;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            return c ^ 0xFFFFFFFF;
        }
    }
}
