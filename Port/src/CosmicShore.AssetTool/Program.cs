using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Editing;
using CosmicShore.Content.Yaml;

namespace CosmicShore.AssetTool
{
    /// <summary>
    /// cs-asset — edit the Unity project's scenes, prefabs and assets without the Unity Editor.
    ///
    ///   cs-asset roundtrip [path…]                      prove every file survives parse → write unchanged
    ///   cs-asset list   &lt;file&gt;                           GameObjects with their hierarchy paths
    ///   cs-asset docs   &lt;file&gt;                           every document (fileID, class, type)
    ///   cs-asset get    &lt;file&gt; &lt;object&gt; [field.path]     print a document or one field
    ///   cs-asset set    &lt;file&gt; &lt;object&gt; &lt;field.path&gt; &lt;yaml value&gt;
    ///   cs-asset remove &lt;file&gt; &lt;object&gt; &lt;field.path&gt;
    ///   cs-asset create &lt;file&gt; &lt;name&gt; [--parent &lt;object&gt;]
    ///   cs-asset delete &lt;file&gt; &lt;object&gt;
    ///   cs-asset components &lt;file&gt; &lt;object&gt;
    ///   cs-asset add    &lt;file&gt; &lt;object&gt; &lt;Component&gt; [--like &amp;fileID]
    ///   cs-asset remove-component &lt;file&gt; &lt;object&gt; --component Type[#n]
    ///   cs-asset instantiate &lt;file&gt; &lt;prefab&gt; [--parent &lt;object&gt;] [--name N] [--position x,y,z]
    ///   cs-asset overrides &lt;file&gt; [&lt;object&gt;]
    ///   cs-asset revert &lt;file&gt; &lt;object&gt; [field.path]
    ///   cs-asset schema [path…]                         measure the component serializer against the project
    ///
    /// &lt;object&gt; is <c>&amp;id</c>, a GameObject hierarchy path (<c>Canvas/Panel/Button</c>) or a
    /// unique GameObject name — reaching inside nested prefab instances, where every edit is
    /// written as that instance's overrides; add <c>--component Type</c> to address that
    /// GameObject's component (Transform, MonoBehaviour, …; <c>Type#2</c> for the second one).
    ///
    /// Writes change the file in place (<c>--dry-run</c> prints the diff and writes nothing) and
    /// touch only the lines the edit changed. Before anything is written the output is parsed
    /// back and must describe the same content the edit produced, or the write is refused.
    /// Close the scene in the Unity Editor first; Unity does not merge external edits to a
    /// scene it has open.
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            var opts = new Dictionary<string, string>();
            var pos = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is "--dry-run" or "--force") { opts[args[i][2..]] = "1"; continue; }
                if (args[i].StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Length) { opts[args[i][2..]] = args[++i]; continue; }
                pos.Add(args[i]);
            }
            if (pos.Count == 0) { Usage(); return 2; }
            try
            {
                return pos[0] switch
                {
                    "roundtrip" => RoundTrip(pos.Skip(1).ToList()),
                    "schema" => Scripts.Schema(pos.Skip(1).ToList()),
                    "addall" => Scripts.AddAll(),
                    "list" => List(Need(pos, 2)),
                    "docs" => Docs(Need(pos, 2)),
                    "get" => Get(Need(pos, 3), opts),
                    "set" => Edit(Need(pos, 5), opts, (s, o) => Set(s, o, pos[3], pos[4], opts)),
                    "remove" => Edit(Need(pos, 4), opts, (s, o) =>
                    {
                        if (o.InInstance) throw new ArgumentException($"&{o.Id} is inside a prefab instance; use 'revert' to drop an override");
                        if (!s.Ed.Remove(o.Id, pos[3])) throw new ArgumentException($"no field '{pos[3]}'");
                    }),
                    "revert" => Revert(Need(pos, 3), opts),
                    "create" => Create(Need(pos, 3), opts),
                    "components" => Components(Need(pos, 3), opts),
                    "add" => AddComponent(Need(pos, 4), opts),
                    "remove-component" => RemoveComponent(Need(pos, 3), opts),
                    "delete" => Delete(Need(pos, 3), opts),
                    "instantiate" => Instantiate(Need(pos, 3), opts),
                    "overrides" => Overrides(Need(pos, 2), opts),
                    _ => Fail($"unknown command '{pos[0]}'"),
                };
            }
            catch (ArgumentException e) { return Fail(e.Message); }
            catch (IOException e) { return Fail(e.Message); }
        }

        static List<string> Need(List<string> pos, int n)
        {
            if (pos.Count < n) throw new ArgumentException($"'{pos[0]}' needs {n - 1} argument(s); run with no arguments for usage");
            return pos;
        }

        static int Fail(string msg) { Console.Error.WriteLine("error: " + msg); return 1; }

        static void Usage() => Console.WriteLine(
@"cs-asset — edit Unity scenes, prefabs and assets without the Unity Editor

  roundtrip [path...]                       verify parse -> write is byte-identical (default: all of Assets/)
  list   <file>                             GameObjects and their hierarchy paths
  docs   <file>                             every document: fileID, class, type
  get    <file> <object> [field.path]       print a document, or one field of it
  set    <file> <object> <field.path> <yaml value>
  remove <file> <object> <field.path>
  create <file> <name> [--parent <object>]
  delete <file> <object>
  components <file> <object>                list a GameObject's components
  add    <file> <object> <Component>        add a script, package (Image, TextMeshProUGUI ...) or built-in
                                            (BoxCollider, Rigidbody ...) component; --like &fileID copies
                                            an existing component in the same file instead
  remove-component <file> <object> --component <Type>[#n]
  instantiate <file> <prefab> [--parent <object>] [--name N] [--position x,y,z]
                                            place a prefab (as the Editor does when one is dropped in)
  overrides <file> [<object>]               what each placed prefab overrides, removes and adds
  revert <file> <object> [field.path]       drop an object's overrides (all of them, or one field's)
  schema [path...]                          check the component serializer against every saved script

<object>: &id | GameObject path (Canvas/Panel/Button) | unique GameObject name
          paths and names reach inside placed prefabs; there, set/add/create/delete/remove-component
          are written as the instance's overrides, exactly as the Unity Editor writes them
          --component <Type>[#n] targets that GameObject's component instead
<yaml value>: 5 | Hello | {x: 0, y: 1, z: 0} | [] | {fileID: 0} | @<object>[:<Type>[#n]] (a reference to it)
--dry-run prints the change and writes nothing.  --force writes an override for a field the prefab lacks.");

        // ── Session: one file, its editor, and its prefab instances ───────

        sealed class Session
        {
            public readonly string Path;
            public readonly UnityAssetEditor Ed;
            public readonly PrefabInstanceEditor Pie;
            public Session(string path)
            {
                Path = System.IO.Path.GetFullPath(path);
                Ed = new UnityAssetEditor(UnityYamlFile.Load(Path));
                Pie = new PrefabInstanceEditor(Ed, Scripts.Db, Path);
            }
            public UnityYamlFile File => Ed.File;
        }

        static string Short(string assetPath) => assetPath == null ? "(missing prefab)" : System.IO.Path.GetFileName(assetPath);

        /// <summary>How a listing marks an object: a placed prefab's root, or an object inside one.</summary>
        static string Where(Session s, SceneObject o)
        {
            if (!o.InInstance) return "";
            string prefab = Short(s.Pie.SourcePrefabPath(o.Instance));
            return s.Pie.InstanceRoot(o.Instance)?.Id == o.Id ? $"  [prefab {prefab}]" : $"  (in {prefab})";
        }

        // ── Read ──────────────────────────────────────────────────────

        static int List(List<string> pos)
        {
            var s = new Session(pos[1]);
            foreach (var (go, path) in s.Pie.GameObjects().OrderBy(g => g.Path, StringComparer.Ordinal))
                Console.WriteLine($"&{go.Id,-22} {path}{Where(s, go)}");
            return 0;
        }

        static int Docs(List<string> pos)
        {
            var f = UnityYamlFile.Load(pos[1]);
            foreach (var d in f.Documents)
                Console.WriteLine($"&{d.FileId,-22} !u!{d.ClassId,-6} {d.TypeName}{(d.Stripped ? " (stripped)" : "")}");
            return 0;
        }

        static int Get(List<string> pos, Dictionary<string, string> opts)
        {
            var s = new Session(pos[1]);
            var o = ResolveObject(s, pos[2], opts);
            if (pos.Count < 4)
            {
                var one = UnityYamlFile.CreateEmpty();
                if (o.InInstance)
                {
                    int n = s.Pie.Overrides(o.Instance).Count(m => m.Target == o.Source);
                    Console.WriteLine($"# inside prefab instance &{o.Instance} of {Scripts.Db.ProjectRelative(s.Pie.SourcePrefabPath(o.Instance) ?? "?")}: the prefab's values with {n} override(s) of this file");
                    one.Documents.Add(new UnityDocument { ClassId = o.ClassId, FileId = o.Id, TypeName = o.TypeName, Body = s.Pie.Body(o) });
                }
                else one.Documents.Add(s.File.Find(o.Id));
                var text = one.Write(canonical: true);
                Console.Write(text[text.IndexOf("---", StringComparison.Ordinal)..]);
                return 0;
            }
            var node = o.InInstance ? s.Pie.Get(o, pos[3]) : s.Ed.Get(o.Id, pos[3]);
            if (node == null) return Fail($"no field '{pos[3]}' on &{o.Id}");
            Console.WriteLine(node.Scalar ?? UnityYamlFile.FormatValue(node));
            return 0;
        }

        static int Components(List<string> pos, Dictionary<string, string> opts)
        {
            var s = new Session(pos[1]);
            var go = ResolveObject(s, pos[2], new());
            foreach (var c in s.Pie.Components(go))
            {
                string origin = c.InInstance ? "" : go.InInstance ? "  (added by this file)" : "";
                Console.WriteLine($"&{c.Id,-22} {ComponentName(s, c)}{origin}");
            }
            return 0;
        }

        static string ComponentName(Session s, SceneObject c)
        {
            if (c.ClassId == 114 && s.Pie.Body(c)?["m_Script"]?.Str("guid") is { } g)
                return Scripts.Catalog.FromGuid(g).Type?.Name ?? $"(missing script {g})";
            return c.TypeName;
        }

        /// <summary>cs-asset overrides: what each placed prefab changes, removes and adds.</summary>
        static int Overrides(List<string> pos, Dictionary<string, string> opts)
        {
            var s = new Session(pos[1]);
            long only = pos.Count > 2 ? ResolveObject(s, pos[2], new()) is { InInstance: true } o ? o.Instance
                                        : throw new ArgumentException($"'{pos[2]}' is not inside a prefab instance") : 0;
            foreach (var d in s.Pie.Instances())
            {
                if (only != 0 && d.FileId != only) continue;
                var root = s.Pie.InstanceRoot(d.FileId);
                var src = s.Pie.Graph.InstanceSource(d.FileId);
                string prefab = s.Pie.SourcePrefabPath(d.FileId);
                Console.WriteLine($"&{d.FileId}  {(prefab == null ? "(missing prefab)" : Scripts.Db.ProjectRelative(prefab))}  as '{(root == null ? "?" : s.Pie.PathOf(root))}'");
                string Describe(long source)
                {
                    var obj = s.Pie.InstanceObject(d.FileId, source);
                    if (obj != null)
                        return (obj.ClassId == UnityAssetEditor.GameObjectClass ? s.Pie.PathOf(obj) : s.Pie.PathOf(s.Pie.GameObjectOf(obj)) + " " + ComponentName(s, obj));
                    // Removed by this instance: name it from the prefab.
                    var so = src?.Get(source);
                    if (so == null) return $"&{source} (not in the prefab: ignored)";
                    var sgo = so.ClassId == UnityAssetEditor.GameObjectClass ? so : src.Get(src.Resolve(so.Body["m_GameObject"]?.Long("fileID") ?? 0));
                    var live = sgo == null ? null : s.Pie.InstanceObject(d.FileId, sgo.Id);
                    string where = live != null ? s.Pie.PathOf(live) : sgo?.Body.Str("m_Name") ?? "?";
                    string type = so.ClassId == 114 && so.Body["m_Script"]?.Str("guid") is { } g
                        ? Scripts.Catalog.FromGuid(g).Type?.Name ?? so.TypeName : so.TypeName;
                    return so.ClassId == UnityAssetEditor.GameObjectClass ? where : $"{where} {type}";
                }
                foreach (var m in s.Pie.Overrides(d.FileId))
                {
                    string v = m.ObjectReference is { } r && r.Long("fileID") != 0 ? "-> " + UnityYamlFile.FormatValue(r) : m.Value;
                    Console.WriteLine($"  {Describe(m.Target)}  .{m.PropertyPath} = {v}");
                }
                var mod = d.Body["m_Modification"];
                foreach (var r in mod?["m_RemovedComponents"]?.Items ?? Array.Empty<YNode>())
                    Console.WriteLine($"  removed component  {Describe(r.Long("fileID"))}");
                foreach (var r in mod?["m_RemovedGameObjects"]?.Items ?? Array.Empty<YNode>())
                    Console.WriteLine($"  removed object     {Describe(r.Long("fileID"))}");
                foreach (var key in new[] { "m_AddedGameObjects", "m_AddedComponents" })
                    foreach (var a in mod?[key]?.Items ?? Array.Empty<YNode>())
                    {
                        long added = a["addedObject"]?.Long("fileID") ?? 0;
                        var ao = s.Pie.Object(added);
                        string what = ao == null ? $"&{added}"
                            : key == "m_AddedGameObjects" ? s.Pie.PathOf(s.Pie.GameObjectOf(ao)) : s.Pie.PathOf(s.Pie.GameObjectOf(ao)) + " " + ComponentName(s, ao);
                        Console.WriteLine($"  {(key == "m_AddedGameObjects" ? "added object    " : "added component ")}  {what}  (&{added})");
                    }
            }
            return 0;
        }

        // ── Write ─────────────────────────────────────────────────────

        static int Edit(List<string> pos, Dictionary<string, string> opts, Action<Session, SceneObject> edit)
        {
            var s = new Session(pos[1]);
            edit(s, ResolveObject(s, pos[2], opts));
            return Save(s.Path, s.File, opts);
        }

        static void Set(Session s, SceneObject o, string path, string raw, Dictionary<string, string> opts)
        {
            var value = ParseValue(s, raw);
            if (o.InInstance)
            {
                s.Pie.SetOverride(o, path, value, force: opts.ContainsKey("force"));
                Console.WriteLine($"override on {Short(s.Pie.SourcePrefabPath(o.Instance))} instance &{o.Instance}");
            }
            else s.Ed.Set(o.Id, path, value);
        }

        /// <summary>A YAML value, or <c>@object</c> (<c>@Canvas/Panel</c>, <c>@&amp;id</c>, <c>@Canvas/Panel:Image#2</c>) for a reference to it.</summary>
        static YNode ParseValue(Session s, string raw)
        {
            if (!raw.StartsWith('@')) return UnityYaml.ParseValue(raw);
            string spec = raw[1..];
            var opts = new Dictionary<string, string>();
            int colon = spec.LastIndexOf(':');
            if (colon > 0) { opts["component"] = spec[(colon + 1)..]; spec = spec[..colon]; }
            var target = ResolveObject(s, spec, opts);
            return YMap.Ref(s.Pie.StandIn(target));
        }

        static int Revert(List<string> pos, Dictionary<string, string> opts)
        {
            var s = new Session(pos[1]);
            var o = ResolveObject(s, pos[2], opts);
            int n = s.Pie.Revert(o, pos.Count > 3 ? pos[3] : null);
            Console.WriteLine(n == 0 ? "no override to revert" : $"reverted {n} override(s)");
            return Save(s.Path, s.File, opts);
        }

        static int Create(List<string> pos, Dictionary<string, string> opts)
        {
            var s = new Session(pos[1]);
            var parent = opts.TryGetValue("parent", out var p) ? ResolveObject(s, p, new()) : null;
            long id = s.Pie.CreateGameObject(pos[2], parent);
            Console.WriteLine($"created &{id} '{pos[2]}'{(parent is { InInstance: true } ? $" (added to {Short(s.Pie.SourcePrefabPath(parent.Instance))} instance &{parent.Instance})" : "")}");
            return Save(s.Path, s.File, opts);
        }

        static int Delete(List<string> pos, Dictionary<string, string> opts)
        {
            var s = new Session(pos[1]);
            var o = ResolveObject(s, pos[2], new());
            bool wholeInstance = o.InInstance && s.Pie.InstanceRoot(o.Instance)?.Id == o.Id;
            var r = s.Pie.Delete(o);
            if (wholeInstance) Console.WriteLine($"removed prefab instance &{o.Instance} ({r.Removed.Count} document(s))");
            else if (o.InInstance) Console.WriteLine($"recorded as removed from {Short(s.Pie.SourcePrefabPath(o.Instance))} instance &{o.Instance}");
            else Console.WriteLine($"removed {r.Removed.Count} document(s)");
            foreach (var c in r.ClearedReferences) Console.WriteLine($"  cleared reference {c}");
            return Save(s.Path, s.File, opts);
        }

        static int RemoveComponent(List<string> pos, Dictionary<string, string> opts)
        {
            if (!opts.ContainsKey("component") && !pos[2].StartsWith('&'))
                throw new ArgumentException("name the component: --component Type[#n], or &fileID");
            var s = new Session(pos[1]);
            var c = ResolveObject(s, pos[2], opts);
            var r = s.Pie.RemoveComponent(c);
            if (c.InInstance) Console.WriteLine($"recorded as removed from {Short(s.Pie.SourcePrefabPath(c.Instance))} instance &{c.Instance}");
            foreach (var x in r.ClearedReferences) Console.WriteLine($"  cleared reference {x}");
            return Save(s.Path, s.File, opts);
        }

        /// <summary>cs-asset instantiate: place a prefab.</summary>
        static int Instantiate(List<string> pos, Dictionary<string, string> opts)
        {
            var s = new Session(pos[1]);
            var parent = opts.TryGetValue("parent", out var p) ? ResolveObject(s, p, new()) : null;
            YMap position = null;
            if (opts.TryGetValue("position", out var at))
            {
                position = at.TrimStart().StartsWith('{') ? UnityYaml.ParseValue(at) as YMap : null;
                if (position == null)
                {
                    var xyz = at.Split(',', StringSplitOptions.TrimEntries);
                    if (xyz.Length != 3) throw new ArgumentException("--position takes x,y,z or {x: …, y: …, z: …}");
                    position = (YMap)UnityYaml.ParseValue($"{{x: {xyz[0]}, y: {xyz[1]}, z: {xyz[2]}}}");
                }
            }
            var root = s.Pie.Instantiate(pos[2], parent, opts.GetValueOrDefault("name"), position);
            Console.WriteLine($"placed prefab instance &{root.Instance} as '{s.Pie.PathOf(root)}' (root &{root.Id})");
            return Save(s.Path, s.File, opts);
        }

        static int Save(string path, UnityYamlFile file, Dictionary<string, string> opts)
        {
            string before = File.ReadAllText(path);
            string after = file.Write();
            // Refuse to write anything that does not read back as what we meant to write.
            if (!UnityYamlFile.SameContent(file, UnityYamlFile.Parse(after)))
                return Fail("the written file does not parse back to the edited content; nothing was written");
            PrintDiff(before, after);
            if (opts.ContainsKey("dry-run")) { Console.WriteLine("(dry run: nothing written)"); return 0; }
            if (before == after) { Console.WriteLine("no change"); return 0; }
            File.WriteAllText(path, after, new System.Text.UTF8Encoding(false));
            Console.WriteLine($"wrote {path}");
            return 0;
        }

        /// <summary>
        /// &amp;id (a document, a stand-in, or an instance object's id from 'list'), a GameObject
        /// path, or a unique GameObject name — inside nested prefab instances as well — then
        /// optionally <c>--component Type[#n]</c> of that GameObject.
        /// </summary>
        static SceneObject ResolveObject(Session s, string spec, Dictionary<string, string> opts)
        {
            SceneObject o;
            if (spec.StartsWith('&') && long.TryParse(spec.AsSpan(1), out long id))
            {
                o = s.Pie.Object(id);
                if (o == null)
                {
                    // A document the loader does not treat as an object (a PrefabInstance, SceneRoots …).
                    var d = s.File.Find(id) ?? throw new ArgumentException($"no object &{id}");
                    if (d.Stripped) throw new ArgumentException($"&{id} stands for an object its prefab instance removes");
                    o = new SceneObject(id, d.ClassId, d.TypeName, 0, 0);
                }
            }
            else
            {
                o = s.Pie.FindGameObject(spec)
                    ?? throw new ArgumentException($"no unique GameObject '{spec}' (use a path like Parent/Child, or &id from 'list')");
            }
            if (!opts.TryGetValue("component", out var comp)) return o;

            string type = comp; int nth = 1;
            int hash = comp.IndexOf('#');
            if (hash > 0) { type = comp[..hash]; nth = int.Parse(comp[(hash + 1)..]); }
            if (o.ClassId != UnityAssetEditor.GameObjectClass) throw new ArgumentException($"&{o.Id} is not a GameObject");
            int seen = 0;
            foreach (var c in s.Pie.Components(o))
                // A script component is "MonoBehaviour" in the file; match its class name too.
                if ((c.TypeName == type || ComponentName(s, c) == type) && ++seen == nth) return c;
            throw new ArgumentException($"&{o.Id} has no component {comp}");
        }

        // ── Components ────────────────────────────────────────────────

        static int AddComponent(List<string> pos, Dictionary<string, string> opts)
        {
            var s = new Session(pos[1]);
            var go = ResolveObject(s, pos[2], new());
            if (go.ClassId != UnityAssetEditor.GameObjectClass) throw new ArgumentException($"&{go.Id} is not a GameObject");
            long target = s.Pie.StandIn(go); // an object inside an instance is added to through its stand-in
            if (opts.TryGetValue("like", out var like))
            {
                var src = ResolveObject(s, like, new());
                if (src.ClassId is UnityAssetEditor.TransformClass or UnityAssetEditor.RectTransformClass or UnityAssetEditor.GameObjectClass or UnityAssetEditor.PrefabInstanceClass)
                    throw new ArgumentException("--like copies a component, not a Transform or GameObject");
                var body = (YMap)s.Pie.Body(src).Clone();
                foreach (var k in new[] { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" })
                    if (body.Has(k)) body.Set(k, YMap.Ref(0));
                long id = s.Ed.AddComponentDocument(target, src.ClassId, src.TypeName, s.Pie.ToFileRefs(body));
                Console.WriteLine($"added &{id} {ComponentName(s, src)} (copy of &{src.Id})");
                return Save(s.Path, s.File, opts);
            }
            var adder = new ComponentAdder(s.Ed, Scripts.Catalog, new ComponentTemplates(Scripts.Db.AssetsRoot));
            var r = adder.Add(target, pos[3]);
            foreach (var (id, type, source) in r.Added) Console.WriteLine($"added &{id} {type}  ({source})");
            foreach (var n in r.Notes) Console.WriteLine($"  note: {n}");
            return Save(s.Path, s.File, opts);
        }

        // ── Verification ──────────────────────────────────────────────

        static int RoundTrip(List<string> paths)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".overrideController", ".mixer", ".physicMaterial", ".lighting", ".playable" };
            if (paths.Count == 0)
            {
                string root = AssetDatabase.FindProjectRoot() ?? throw new ArgumentException("no Unity project found above the current directory");
                paths.Add(Path.Combine(root, "Assets"));
            }
            var files = paths.SelectMany(p => Directory.Exists(p)
                ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories).Where(f => exts.Contains(Path.GetExtension(f)))
                : new[] { p }).ToList();
            int yaml = 0, lossless = 0, canonicalSame = 0, canonicalReads = 0;
            var failures = new List<string>();
            foreach (var f in files)
            {
                string text = File.ReadAllText(f);
                if (!text.StartsWith("%YAML", StringComparison.Ordinal)) continue;
                yaml++;
                var file = UnityYamlFile.Parse(text);
                string written = file.Write();
                if (written == text) lossless++;
                else failures.Add(f);
                string canon = file.Write(canonical: true);
                if (canon == text.Replace("\r\n", "\n")) canonicalSame++;
                if (UnityYamlFile.SameContent(file, UnityYamlFile.Parse(canon))) canonicalReads++;
                else failures.Add(f + " (canonical output does not read back)");
            }
            Console.WriteLine($"{yaml} YAML files");
            Console.WriteLine($"  lossless round-trip (parse -> write, byte-identical): {lossless}/{yaml}");
            Console.WriteLine($"  full re-emit reads back as the same content:         {canonicalReads}/{yaml}");
            Console.WriteLine($"  full re-emit byte-identical to the file on disk:     {canonicalSame}/{yaml}");
            foreach (var f in failures.Take(20)) Console.WriteLine("  FAIL " + f);
            return failures.Count == 0 ? 0 : 1;
        }

        // ── Diff (Myers, line-based; enough to show what an edit touched) ─────

        static void PrintDiff(string a, string b)
        {
            if (a == b) return;
            var x = a.Split('\n'); var y = b.Split('\n');
            int pre = 0;
            while (pre < x.Length && pre < y.Length && x[pre] == y[pre]) pre++;
            int suf = 0;
            while (suf < x.Length - pre && suf < y.Length - pre && x[^(suf + 1)] == y[^(suf + 1)]) suf++;
            var xs = x[pre..(x.Length - suf)]; var ys = y[pre..(y.Length - suf)];
            var ops = Myers(xs, ys);
            int shown = 0, xi = 0, yi = 0;
            foreach (var op in ops)
            {
                if (shown > 200) { Console.WriteLine("  … (diff truncated)"); break; }
                if (op == ' ') { xi++; yi++; continue; }
                if (op == '-') { Console.WriteLine($"  -{pre + xi + 1,7}: {xs[xi]}"); xi++; }
                else { Console.WriteLine($"  +{pre + yi + 1,7}: {ys[yi]}"); yi++; }
                shown++;
            }
        }

        static List<char> Myers(string[] a, string[] b)
        {
            int n = a.Length, m = b.Length, max = n + m;
            var v = new int[2 * max + 2];
            var trace = new List<int[]>();
            for (int d = 0; d <= max; d++)
            {
                trace.Add((int[])v.Clone());
                for (int k = -d; k <= d; k += 2)
                {
                    int xx = (k == -d || (k != d && v[max + k - 1] < v[max + k + 1])) ? v[max + k + 1] : v[max + k - 1] + 1;
                    int yy = xx - k;
                    while (xx < n && yy < m && a[xx] == b[yy]) { xx++; yy++; }
                    v[max + k] = xx;
                    if (xx >= n && yy >= m) return Backtrack(trace, a, b, max, d);
                }
            }
            return new List<char>();
        }

        static List<char> Backtrack(List<int[]> trace, string[] a, string[] b, int max, int dEnd)
        {
            var ops = new List<char>();
            int x = a.Length, y = b.Length;
            for (int d = dEnd; d > 0; d--)
            {
                var v = trace[d];
                int k = x - y;
                int pk = (k == -d || (k != d && v[max + k - 1] < v[max + k + 1])) ? k + 1 : k - 1;
                int px = v[max + pk], py = px - pk;
                while (x > px && y > py) { ops.Add(' '); x--; y--; }
                ops.Add(x == px ? '+' : '-');
                if (x == px) y--; else x--;
            }
            while (x > 0 && y > 0) { ops.Add(' '); x--; y--; }
            ops.Reverse();
            return ops;
        }
    }
}
