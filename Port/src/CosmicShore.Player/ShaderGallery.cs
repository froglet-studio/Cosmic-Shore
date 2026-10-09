using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Content;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// <c>--shader-gallery FRAME[:LEGEND]</c>: at FRAME, one real project material per first-party
    /// Shader Graph that no hand-tuned family covers (and per hand-translated .shader; only those with
    /// COSMIC_SHORE_GALLERY=hand), each on a sphere in a grid in front of a
    /// camera of its own (every other camera is switched off), so the same frame of two builds
    /// compares shader by shader. The legend (cell → graph → material) goes to the console and
    /// LEGEND. A proof harness for C2: what the graph compiler draws, before and after.
    /// </summary>
    static class ShaderGallery
    {
        static readonly Regex s_shaderRef = new(@"m_Shader:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);

        public static void Build(string legendPath)
        {
            var content = ContentRuntime.Current;
            if (content == null) { Console.WriteLine("[gallery] content not booted yet"); return; }
            var db = content.Db;
            // The first material (by path) using each graph.
            var byGraph = new SortedDictionary<string, (string Graph, string MatGuid, string MatPath)>(StringComparer.OrdinalIgnoreCase);
            foreach (var mat in db.AllAssetPaths.Where(p => p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) && p.StartsWith(db.AssetsRoot, StringComparison.OrdinalIgnoreCase))
                                                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string text;
                try { text = File.ReadAllText(mat); } catch (IOException) { continue; }
                var m = s_shaderRef.Match(text);
                if (!m.Success) continue;
                var guid = m.Groups[1].Value;
                if (CosmicShore.Render.MaterialFamilies.ByGuid.ContainsKey(guid)) continue;
                var graph = db.PathOf(guid);
                if (graph == null) continue;
                bool hand = graph.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) && CosmicShore.Content.Shaders.HandShaders.Has(guid);
                if (!hand && !graph.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase)) continue;
                // COSMIC_SHORE_GALLERY=hand: only the hand-translated .shader files.
                if (!hand && Environment.GetEnvironmentVariable("COSMIC_SHORE_GALLERY") == "hand") continue;
                var rel = db.ProjectRelative(graph);
                if (!byGraph.ContainsKey(rel)) byGraph[rel] = (rel, db.GuidOf(mat), db.ProjectRelative(mat));
            }

            foreach (var other in Camera.allCameras) other.enabled = false;
            // The scene's screen-space UI would cover the grid.
            foreach (var canvas in CosmicShore.Engine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
            var root = new GameObject("ShaderGallery");
            root.transform.position = new Vector3(0, 20000, 0);
            int n = byGraph.Count, cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(n * 16.0 / 9.0)));
            int rows = Math.Max(1, (n + cols - 1) / cols);
            const float pitch = 2.4f;
            var legend = new List<string>();
            int i = 0;
            foreach (var (graph, matGuid, matPath) in byGraph.Values)
            {
                var material = content.Assets.Load<Material>(new ObjRef(2100000, matGuid, 2));
                if (material == null) continue;
                int c = i % cols, r = i / cols;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Gallery " + i;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3((c - (cols - 1) * 0.5f) * pitch, ((rows - 1) * 0.5f - r) * pitch, 0);
                go.transform.localScale = Vector3.one * 2f;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                legend.Add($"{i,3}  row {r} col {c}  {Path.GetFileName(graph),-40} {matPath}");
                i++;
            }
            var camGo = new GameObject("GalleryCamera") { tag = "MainCamera" };
            camGo.transform.SetParent(root.transform, false);
            float halfH = rows * pitch * 0.5f + 0.5f;
            camGo.transform.localPosition = new Vector3(0, 0, -halfH / MathF.Tan(30f * MathF.PI / 180f) - 1f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.depth = 100;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.22f, 1f);
            Console.WriteLine($"[gallery] {i} graphs on a {cols}x{rows} grid:");
            foreach (var l in legend) Console.WriteLine("[gallery] " + l);
            if (!string.IsNullOrEmpty(legendPath)) File.WriteAllLines(legendPath, legend);
        }
    }
}
