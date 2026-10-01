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
    ///   cs-asset schema [path…]                         measure the component serializer against the project
    ///
    /// &lt;object&gt; is <c>&amp;fileID</c>, a GameObject hierarchy path (<c>Canvas/Panel/Button</c>) or a
    /// unique GameObject name; add <c>--component Type</c> to address that GameObject's
    /// component (Transform, MonoBehaviour, …; <c>Type#2</c> for the second one).
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
                if (args[i] == "--dry-run") { opts["dry-run"] = "1"; continue; }
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
                    "set" => Edit(Need(pos, 5), opts, (ed, id) => ed.Set(id, pos[3], pos[4])),
                    "remove" => Edit(Need(pos, 4), opts, (ed, id) =>
                    {
                        if (!ed.Remove(id, pos[3])) throw new ArgumentException($"no field '{pos[3]}'");
                    }),
                    "create" => Create(Need(pos, 3), opts),
                    "components" => Components(Need(pos, 3), opts),
                    "add" => AddComponent(Need(pos, 4), opts),
                    "remove-component" => Edit(Need(pos, 3), opts, (ed, id) =>
                    {
                        if (!opts.ContainsKey("component") && !pos[2].StartsWith('&'))
                            throw new ArgumentException("name the component: --component Type[#n], or &fileID");
                        var r = ed.RemoveComponent(id);
                        foreach (var c in r.ClearedReferences) Console.WriteLine($"  cleared reference {c}");
                    }),
                    "delete" => Delete(Need(pos, 3), opts),
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
  schema [path...]                          check the component serializer against every saved script

<object>: &fileID | GameObject path (Canvas/Panel/Button) | unique GameObject name
          --component <Type>[#n] targets that GameObject's component instead
--dry-run prints the change and writes nothing.");

        // ── Read ──────────────────────────────────────────────────────

        static int List(List<string> pos)
        {
            var ed = new UnityAssetEditor(UnityYamlFile.Load(pos[1]));
            var all = ed.GameObjects();
            var byT = all.Where(g => g.TransformId != 0).GroupBy(g => g.TransformId).ToDictionary(g => g.Key, g => g.First());
            string PathOf(GameObjectInfo g)
            {
                var n = new List<string>();
                for (int guard = 0; guard < 256; guard++)
                {
                    n.Add(g.Name);
                    if (g.ParentTransformId == 0 || !byT.TryGetValue(g.ParentTransformId, out g)) break;
                }
                n.Reverse();
                return string.Join("/", n);
            }
            foreach (var g in all.OrderBy(PathOf, StringComparer.Ordinal))
                Console.WriteLine($"&{g.FileId,-22} {PathOf(g)}{(g.FromPrefabInstance ? "  (in a nested prefab)" : "")}");
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
            var file = UnityYamlFile.Load(pos[1]);
            var ed = new UnityAssetEditor(file);
            long id = ResolveObject(ed, pos[2], opts);
            if (pos.Count < 4)
            {
                var one = UnityYamlFile.CreateEmpty();
                one.Documents.Add(file.Find(id));
                var text = one.Write(canonical: true);
                Console.Write(text[(text.IndexOf("---", StringComparison.Ordinal))..]);
                return 0;
            }
            var node = ed.Get(id, pos[3]);
            if (node == null) return Fail($"no field '{pos[3]}' on &{id}");
            Console.WriteLine(node.Scalar ?? UnityYamlFile.FormatValue(node));
            return 0;
        }

        // ── Write ─────────────────────────────────────────────────────

        static int Edit(List<string> pos, Dictionary<string, string> opts, Action<UnityAssetEditor, long> edit)
        {
            string path = pos[1];
            var ed = new UnityAssetEditor(UnityYamlFile.Load(path));
            long id = ResolveObject(ed, pos[2], opts);
            edit(ed, id);
            return Save(path, ed.File, opts);
        }

        static int Create(List<string> pos, Dictionary<string, string> opts)
        {
            string path = pos[1];
            var ed = new UnityAssetEditor(UnityYamlFile.Load(path));
            long parent = opts.TryGetValue("parent", out var p) ? ResolveObject(ed, p, new()) : 0;
            long id = ed.CreateGameObject(pos[2], parent);
            Console.WriteLine($"created &{id} '{pos[2]}'");
            return Save(path, ed.File, opts);
        }

        static int Delete(List<string> pos, Dictionary<string, string> opts)
        {
            string path = pos[1];
            var ed = new UnityAssetEditor(UnityYamlFile.Load(path));
            long id = ResolveObject(ed, pos[2], new());
            var r = ed.DeleteGameObject(id);
            Console.WriteLine($"removed {r.Removed.Count} document(s)");
            foreach (var c in r.ClearedReferences) Console.WriteLine($"  cleared reference {c}");
            return Save(path, ed.File, opts);
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

        static long ResolveObject(UnityAssetEditor ed, string spec, Dictionary<string, string> opts)
        {
            long id;
            if (spec.StartsWith('&') && long.TryParse(spec.AsSpan(1), out id))
            {
                if (ed.File.Find(id) == null) throw new ArgumentException($"no document &{id}");
            }
            else
            {
                id = ed.FindGameObject(spec);
                if (id == 0) throw new ArgumentException($"no unique GameObject '{spec}' (use a path like Parent/Child, or &fileID from 'list')");
            }
            if (!opts.TryGetValue("component", out var comp)) return id;

            string type = comp; int nth = 1;
            int hash = comp.IndexOf('#');
            if (hash > 0) { type = comp[..hash]; nth = int.Parse(comp[(hash + 1)..]); }
            var go = ed.File.Find(id);
            if (go?.Body["m_Component"] is not YSeq comps) throw new ArgumentException($"&{id} is not a GameObject");
            int seen = 0;
            foreach (var c in comps.Items)
            {
                long cid = c["component"] is YMap m ? m.Long("fileID") : 0;
                if (ed.File.Find(cid) is not { } cd) continue;
                // A script component is "MonoBehaviour" in the file; match its class name too.
                string scriptName = cd.ClassId == 114 && cd.Body["m_Script"]?.Str("guid") is { } g ? Scripts.Catalog.FromGuid(g).Type?.Name : null;
                if ((cd.TypeName == type || scriptName == type) && ++seen == nth) return cid;
            }
            throw new ArgumentException($"&{id} has no component {comp}");
        }

        // ── Components ────────────────────────────────────────────────

        static int Components(List<string> pos, Dictionary<string, string> opts)
        {
            var ed = new UnityAssetEditor(UnityYamlFile.Load(pos[1]));
            long go = ResolveObject(ed, pos[2], new());
            foreach (var c in ed.Components(go))
            {
                string name = c.TypeName;
                if (c.ClassId == 114 && c.Body["m_Script"]?.Str("guid") is { } g)
                    name = Scripts.Catalog.FromGuid(g).Type?.Name ?? $"(missing script {g})";
                Console.WriteLine($"&{c.FileId,-22} {name}");
            }
            return 0;
        }

        static int AddComponent(List<string> pos, Dictionary<string, string> opts)
        {
            string path = pos[1];
            var ed = new UnityAssetEditor(UnityYamlFile.Load(path));
            long go = ResolveObject(ed, pos[2], new());
            if (opts.TryGetValue("like", out var like))
            {
                if (!like.StartsWith('&') || !long.TryParse(like.AsSpan(1), out long src) || ed.File.Find(src) is not { } doc)
                    throw new ArgumentException($"--like needs a component &fileID in this file");
                if (doc.ClassId is UnityAssetEditor.TransformClass or UnityAssetEditor.RectTransformClass or UnityAssetEditor.GameObjectClass)
                    throw new ArgumentException("--like copies a component, not a Transform or GameObject");
                var body = (YMap)doc.Body.Clone();
                long id = ed.AddComponentDocument(go, doc.ClassId, doc.TypeName, body);
                Console.WriteLine($"added &{id} {doc.TypeName} (copy of &{src})");
                return Save(path, ed.File, opts);
            }
            var adder = new ComponentAdder(ed, Scripts.Catalog, new ComponentTemplates(Scripts.Db.AssetsRoot));
            var r = adder.Add(go, pos[3]);
            foreach (var (id, type, source) in r.Added) Console.WriteLine($"added &{id} {type}  ({source})");
            foreach (var n in r.Notes) Console.WriteLine($"  note: {n}");
            return Save(path, ed.File, opts);
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
