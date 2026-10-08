using System;
using System.IO;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Shaders;
using CosmicShore.Render;

namespace CosmicShore.Player
{
    /// <summary>
    /// <c>--check-shaders</c>: every Shader Graph in the project (and every hand translation of a
    /// hand-written .shader), compiled by the content layer and
    /// linked on THIS context through the renderer's own template, as desktop GLSL and (when the
    /// driver accepts GLSL ES, GL_ARB_ES3_compatibility) as the phones' GLSL ES 3.00. Prints one
    /// line per failure and a summary; the exit code is the number of failures.
    /// </summary>
    static class ShaderCheck
    {
        public static int Run(Func<CosmicShore.Engine.ShaderGraphProgram, bool, string> link)
        {
            var root = AssetDatabase.FindProjectRoot();
            if (root == null) { Console.WriteLine("[shader-check] no project root (set COSMIC_SHORE_PROJECT)"); return 1; }
            var db = new AssetDatabase(root);
            var catalog = new ShaderGraphCatalog(db);
            // Every graph, then every hand-written .shader that has a hand translation (HandShaders).
            var graphs = db.AllAssetPaths.Where(p => (p.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase)
                                                      || (p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) && HandShaders.Has(db.GuidOf(p))))
                                                     && p.StartsWith(db.AssetsRoot, StringComparison.OrdinalIgnoreCase))
                                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            bool es = GlCaps.Has("GL_ARB_ES3_compatibility");
            int ok = 0, failed = 0, approximate = 0;
            foreach (var path in graphs)
            {
                var rel = db.ProjectRelative(path);
                var prog = catalog.For(db.GuidOf(path));
                if (prog == null) { Console.WriteLine($"[shader-check] SKIP {rel}: not a compilable graph"); continue; }
                if (!prog.Ok) { failed++; Console.WriteLine($"[shader-check] FAIL {rel}: compiler: {prog.Error}"); continue; }
                bool good = true;
                foreach (bool asEs in es ? new[] { false, true } : new[] { false })
                {
                    var error = link(prog, asEs);
                    if (error == null) continue;
                    good = false;
                    Console.WriteLine($"[shader-check] FAIL {rel} ({(asEs ? "GLSL ES 3.00" : "GLSL 3.30")}): {error}");
                    break;
                }
                if (good)
                {
                    ok++;
                    if (prog.Approximations.Count > 0) { approximate++; Console.WriteLine($"[shader-check] ok   {rel} (approximate: {string.Join("; ", prog.Approximations)})"); }
                }
                else failed++;
            }
            Console.WriteLine($"[shader-check] {ok} of {ok + failed} graphs and hand shaders linked ({approximate} with approximations){(es ? ", desktop and ES" : ", desktop only (no GL_ARB_ES3_compatibility)")}; {failed} failed");
            return failed;
        }
    }
}
