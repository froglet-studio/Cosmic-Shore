using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Engine;

namespace CosmicShore.Render
{
    /// <summary>
    /// The hand-tuned material families: the scene program's own translations of the hottest
    /// shaders (prisms, vessels, crystals, spindles ...), keyed by the shader ASSET's guid so a
    /// renamed graph keeps its family. These win over the Shader Graph compiler (the fast path);
    /// everything else compiles from its graph, and a shader neither covers warns once.
    /// </summary>
    public static class MaterialFamilies
    {
        public enum Kind
        {
            None = 0,
            BlockGraph = 1,          // fresnel pair + the prism vertex chain
            ExplodingBlockGraph = 2, // fresnel pair + the debris chain
            Snow = 3,
            Cage = 4,
            Spindle = 5,
            Crystal = 6,
            Forcefield = 7,
            PrismSlice = 8,
            Vessel = 9,
        }

        /// <summary>guid → (family, the asset it was written against).</summary>
        public static readonly IReadOnlyDictionary<string, (Kind Kind, string Path)> ByGuid = new Dictionary<string, (Kind, string)>(StringComparer.Ordinal)
        {
            ["bf8c159f627e64b439094797bff88611"] = (Kind.BlockGraph, "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph"),
            ["de59ec1f616f51044a23e6c1368d6660"] = (Kind.ExplodingBlockGraph, "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph"),
            ["85c2251bbb0f9b746807624041e02ff3"] = (Kind.Snow, "Assets/_Graphics/Materials/Graphs/SnowGraph.shadergraph"),
            ["6bb03b2998dc62b4b8f9964d5fe5c97e"] = (Kind.Cage, "Assets/_Graphics/Materials/Graphs/CageGraph.shadergraph"),
            ["545a7b2ee58b90744bdb1c9e265a09f4"] = (Kind.Spindle, "Assets/_Graphics/Materials/Graphs/SpindleGraph.shadergraph"),
            ["11986b08af564954bb111977a5cf4dac"] = (Kind.Crystal, "Assets/_Graphics/Materials/Graphs/CrystalGraph.shadergraph"),
            ["0d2382aa91a61b6220c03dc1763fd26f"] = (Kind.Forcefield, "Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.shader"),
            ["7056edc966584b3b99b6509922ebffe6"] = (Kind.PrismSlice, "Assets/_Graphics/Materials/Graphs/PrismSlice.shader"),
            ["0236447d5f06b0541a43756d85b5f222"] = (Kind.Vessel, "Assets/_Graphics/Materials/Graphs/VesselGraph.shadergraph"),
        };

        /// <summary>The hand-tuned family of a shader, by guid (a shader with no asset has none).</summary>
        public static Kind For(Shader shader)
            => shader?.Guid != null && ByGuid.TryGetValue(shader.Guid, out var f) ? f.Kind : Kind.None;

        /// <summary>
        /// Built-in shader names the generic families draw on purpose (URP Lit/Unlit, Standard, the
        /// legacy and UI built-ins): these are covered, not unknown.
        /// </summary>
        public static bool IsKnownBuiltin(string name)
            => name != null && (name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)
                || name == "Standard" || name.StartsWith("Standard ", StringComparison.Ordinal)
                || name.StartsWith("Legacy Shaders/", StringComparison.Ordinal) || name.StartsWith("Unlit/", StringComparison.Ordinal)
                || name.StartsWith("Sprites/", StringComparison.Ordinal) || name.StartsWith("UI/", StringComparison.Ordinal)
                || name.StartsWith("Hidden/", StringComparison.Ordinal) || name.StartsWith("TextMeshPro/", StringComparison.Ordinal)
                || name.StartsWith("Particles/", StringComparison.Ordinal) || name.StartsWith("Mobile/", StringComparison.Ordinal));

        static readonly HashSet<string> s_warned = new(StringComparer.Ordinal);

        /// <summary>Warnings issued so far (one per shader).</summary>
        public static int WarnedCount { get { lock (s_warned) return s_warned.Count; } }

        /// <summary>Receives the one-time warning (the engine log by default).</summary>
        public static Action<string> Warn = msg => Debug.LogWarning(msg);

        /// <summary>
        /// Warns once per shader that it has no translation (no hand-tuned family, no compiled graph)
        /// and names what it draws as instead. Returns true when this call issued the warning.
        /// </summary>
        public static bool WarnUntranslated(Shader shader, string drawsAs, string why = null)
        {
            if (shader == null || IsKnownBuiltin(shader.name)) return false;
            var key = shader.Guid ?? shader.name;
            lock (s_warned) if (!s_warned.Add(key)) return false;
            Warn?.Invoke($"[Render] shader '{shader.name}' ({shader.AssetPath ?? "no asset"}) has no translation; drawing it as {drawsAs}{(why != null ? " (" + why + ")" : "")}.");
            return true;
        }

        /// <summary>Test hook: forget which shaders already warned.</summary>
        public static void ResetWarnings() { lock (s_warned) s_warned.Clear(); }
    }

    /// <summary>
    /// What the scene pass drew, per shader and scene: the input that ranks which untranslated
    /// shader to port next (the session report's <c>render.shaders</c>).
    /// </summary>
    public static class ShaderDrawStats
    {
        public sealed class Row
        {
            public string Scene, Shader, Guid, Path, Route;
            public long Instances;
            public int Frames, Peak;
            internal int LastFrame = -1, ThisFrame;
        }

        static readonly Dictionary<(string, string), Row> s_rows = new();
        static int s_frame;
        static string s_scene = "";

        /// <summary>Called once per rendered frame before any draw.</summary>
        public static void BeginFrame(string scene) { s_frame++; s_scene = scene ?? ""; }

        public static void Count(Shader shader, string route, int instances)
        {
            if (shader == null) return;
            var key = (s_scene, shader.Guid ?? shader.name);
            lock (s_rows)
            {
                if (!s_rows.TryGetValue(key, out var r))
                    s_rows[key] = r = new Row { Scene = s_scene, Shader = shader.name, Guid = shader.Guid, Path = shader.AssetPath, Route = route };
                r.Route = route;
                r.Instances += instances;
                if (r.LastFrame != s_frame) { r.LastFrame = s_frame; r.Frames++; r.ThisFrame = 0; }
                r.ThisFrame += instances;
                if (r.ThisFrame > r.Peak) r.Peak = r.ThisFrame;
            }
        }

        public static List<Row> Snapshot()
        {
            lock (s_rows) return s_rows.Values.OrderByDescending(r => r.Instances).ToList();
        }

        public static void Reset() { lock (s_rows) s_rows.Clear(); }
    }
}
