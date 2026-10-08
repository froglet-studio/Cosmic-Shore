using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Content;
using CosmicShore.Content.Shaders;
using CosmicShore.Render;

namespace CosmicShore.AssetTool
{
    /// <summary>
    /// <c>cs-asset shadergraph-census [--json PATH]</c>: what the Shader Graph compiler covers in
    /// this checkout. Node types by instance count with whether an emitter exists; custom functions
    /// with whether the GLSL library ports them; and every first-party shader's route (hand-tuned
    /// family, compiled graph, or missing). The JSON (default <c>Port/parity/shaders.json</c>) is
    /// what <c>tools/gen_parity_scoreboard.py</c> reads for the scoreboard's shader rows.
    /// Read-only over Assets/.
    /// </summary>
    static class ShaderCensus
    {
        public static int Run(Dictionary<string, string> opts)
        {
            var root = AssetDatabase.FindProjectRoot() ?? throw new InvalidOperationException("no project root");
            var db = new AssetDatabase(root);
            var catalog = new ShaderGraphCatalog(db);
            var supported = new HashSet<string>(ShaderGraphCompiler.SupportedNodeTypes.Concat(ShaderGraphCompiler.StructuralNodeTypes));

            var nodeCounts = new SortedDictionary<string, (int Instances, HashSet<string> Files)>(StringComparer.Ordinal);
            var functions = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int graphFiles = 0, subGraphFiles = 0;
            var paths = db.AllAssetPaths.Where(p => p.StartsWith(db.AssetsRoot, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var p in paths.Where(ShaderGraphCatalog.IsGraphPath))
            {
                ShaderGraphAsset g;
                try { g = ShaderGraphAsset.Load(p); }
                catch (Exception e) { Console.WriteLine($"unreadable: {db.ProjectRelative(p)}: {e.Message}"); continue; }
                if (g.IsSubGraph) subGraphFiles++; else graphFiles++;
                foreach (var n in g.Nodes)
                {
                    var (count, files) = nodeCounts.TryGetValue(n.Type, out var c) ? c : (0, new HashSet<string>());
                    files.Add(p);
                    nodeCounts[n.Type] = (count + 1, files);
                    if (n.Type == "CustomFunctionNode" && n.Int("m_SourceType") == 0)
                    {
                        var fn = n.Str("m_FunctionName") + "_float";
                        functions[fn] = functions.TryGetValue(fn, out var k) ? k + 1 : 1;
                    }
                }
            }

            var shaders = new List<object>();
            int family = 0, compiled = 0, missing = 0;
            foreach (var p in paths.Where(p => p.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)))
            {
                var guid = db.GuidOf(p);
                var rel = db.ProjectRelative(p);
                string route, why;
                var approx = new List<string>();
                if (guid != null && MaterialFamilies.ByGuid.TryGetValue(guid, out var f)) { route = "family"; why = "hand-tuned family " + f.Kind; family++; }
                else if (p.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) && catalog.For(guid) is { } prog)
                {
                    if (prog.Ok) { route = "compiled"; why = "Shader Graph compiler"; approx.AddRange(prog.Approximations); compiled++; }
                    else { route = "missing"; why = "graph did not compile: " + prog.Error; missing++; }
                }
                else { route = "missing"; why = "hand-written .shader: no translation yet"; missing++; }
                shaders.Add(new { path = rel, guid, name = MaterialImporter.ShaderNameFor(new ObjRef(4800000, guid, 3), db), route, why, approximations = approx });
            }

            int instances = nodeCounts.Values.Sum(v => v.Instances);
            var unsupported = nodeCounts.Where(kv => !supported.Contains(kv.Key)).Select(kv => kv.Key).ToList();
            var unported = functions.Keys.Where(fn => !ShaderGraphLibrary.Defines(fn)).ToList();
            Console.WriteLine($"{graphFiles} graphs, {subGraphFiles} sub-graphs, {instances} node instances, {nodeCounts.Count} node types ({unsupported.Count} without an emitter)");
            foreach (var (type, (count, files)) in nodeCounts.OrderByDescending(kv => kv.Value.Instances))
                Console.WriteLine($"  {count,5} in {files.Count,3} files  {type}{(supported.Contains(type) ? "" : "   <-- NO EMITTER")}{(ShaderGraphCompiler.ApproximateNodeTypes.TryGetValue(type, out var a) ? "   (approximate: " + a + ")" : "")}");
            Console.WriteLine($"{functions.Values.Sum()} custom function calls, {functions.Count} distinct functions ({unported.Count} not in ShaderGraphLibrary.glsl)");
            foreach (var (fn, count) in functions.OrderByDescending(kv => kv.Value))
                Console.WriteLine($"  {count,5}  {fn}{(ShaderGraphLibrary.Defines(fn) ? "" : "   <-- NOT PORTED")}");
            Console.WriteLine($"{shaders.Count} first-party shaders: {family} hand-tuned family, {compiled} compiled from the graph, {missing} missing");

            var json = opts.TryGetValue("json", out var j) && j != "1" ? j : Path.Combine(root, "Port", "parity", "shaders.json");
            var doc = new
            {
                source = "cs-asset shadergraph-census",
                graphs = graphFiles, subGraphs = subGraphFiles, nodeInstances = instances,
                nodeTypes = nodeCounts.OrderByDescending(kv => kv.Value.Instances).Select(kv => new { type = kv.Key, instances = kv.Value.Instances, files = kv.Value.Files.Count, emitter = supported.Contains(kv.Key) }).ToList(),
                customFunctions = functions.Select(kv => new { function = kv.Key, calls = kv.Value, ported = ShaderGraphLibrary.Defines(kv.Key) }).ToList(),
                shaders,
            };
            File.WriteAllText(json, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine("wrote " + db.ProjectRelative(json));
            return unsupported.Count + unported.Count;
        }
    }
}
