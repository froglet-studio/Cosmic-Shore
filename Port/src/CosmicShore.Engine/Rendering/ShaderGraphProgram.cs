using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// What the content layer compiled from one Shader Graph: GLSL for each stage against the
    /// renderer's documented symbol contract (see <c>CosmicShore.Content.Shaders.ShaderGraphCompiler</c>),
    /// plus the graph target's render state. Pure data, so the engine carries it without knowing
    /// the backend; the renderer wraps it in its own template and links it.
    /// </summary>
    public sealed class ShaderGraphProgram
    {
        /// <summary>The graph asset's guid (the identity materials reference).</summary>
        public string Guid;
        public string AssetPath;
        public string Name;

        /// <summary>Uniform declarations for the graph's properties, shared by both stages.</summary>
        public string Uniforms = "";
        /// <summary>Helper functions the graph's nodes and sub-graphs need, shared by both stages.</summary>
        public string Functions = "";
        /// <summary>Defines <c>void sg_vertex(out vec3 posOS, out vec3 nrmOS, out vec3 tanOS)</c>.</summary>
        public string Vertex = "";
        /// <summary>Defines <c>SgSurface sg_surface()</c>.</summary>
        public string Fragment = "";

        /// <summary>The library functions (custom function nodes) the graph calls.</summary>
        public readonly List<string> LibraryCalls = new();
        /// <summary>Texture properties and the default each binds when a material has none (white, black, grey, bump).</summary>
        public readonly Dictionary<string, string> TextureDefaults = new(StringComparer.Ordinal);
        /// <summary>Texture properties whose graph default is a texture asset: property reference → asset guid.</summary>
        public readonly Dictionary<string, string> TextureAssetDefaults = new(StringComparer.Ordinal);
        /// <summary>Uniforms that sample a texture asset the graph names directly: uniform → asset guid.</summary>
        public readonly Dictionary<string, string> AssetTextures = new(StringComparer.Ordinal);
        /// <summary>Uniforms that carry a property: GLSL name → the property's reference (material / global name).</summary>
        public readonly Dictionary<string, string> PropertyUniforms = new(StringComparer.Ordinal);

        // ── The target's render state (URP: UniversalTarget + sub-target) ──
        public bool Lit;
        public bool Transparent;
        /// <summary>0 alpha, 1 premultiply, 2 additive, 3 multiply (URP AlphaMode).</summary>
        public int AlphaMode;
        public bool AlphaClip;
        /// <summary>0 off, 1 front, 2 back: what the target's render face culls.</summary>
        public int Cull = 2;
        /// <summary>-1 automatic (opaque writes, transparent does not), 0 off, 1 on.</summary>
        public int ZWrite = -1;
        /// <summary>The target lets each material override surface, blend and cull.</summary>
        public bool AllowMaterialOverride;

        /// <summary>Nodes the compiler could not translate exactly: "Type: reason". Empty when every node translated.</summary>
        public readonly List<string> Approximations = new();
        /// <summary>Why compilation failed; null when the program is usable.</summary>
        public string Error;

        public bool Ok => Error == null;
    }

    public sealed partial class Shader
    {
        /// <summary>Port: the guid of the shader asset materials reference (null for built-ins).</summary>
        public string Guid { get; private set; }

        /// <summary>Port: the project path of the shader asset (null for built-ins).</summary>
        public string AssetPath { get; private set; }

        /// <summary>
        /// Port: <see cref="Find"/> plus the asset identity the content layer resolved. The first
        /// asset to claim a name keeps it, so two assets declaring one name keep the first guid.
        /// </summary>
        public static Shader Find(string name, string guid, string assetPath)
        {
            var shader = Find(name);
            if (shader.Guid == null && !string.IsNullOrEmpty(guid)) { shader.Guid = guid; shader.AssetPath = assetPath; }
            return shader;
        }

        /// <summary>Port hook: loads a texture asset by guid (a graph's own default textures). Set by the content layer.</summary>
        public static Func<string, Texture> TextureByGuid { get; set; }

        static Func<Shader, ShaderGraphProgram> s_graphCompiler;
        ShaderGraphProgram _graph;
        bool _graphResolved;

        /// <summary>Port hook: compiles a shader's graph source. Set by the content layer.</summary>
        public static Func<Shader, ShaderGraphProgram> GraphCompiler
        {
            get => s_graphCompiler;
            set
            {
                s_graphCompiler = value;
                foreach (var s in Registry.Values) { s._graph = null; s._graphResolved = false; }
            }
        }

        /// <summary>Port: this shader's compiled graph, or null when it is not a graph (or no compiler is set). Compiled once.</summary>
        public ShaderGraphProgram GraphProgram
        {
            get
            {
                if (_graphResolved) return _graph;
                _graphResolved = true;
                try { _graph = s_graphCompiler?.Invoke(this); }
                catch (Exception e) { _graph = new ShaderGraphProgram { Guid = Guid, AssetPath = AssetPath, Name = name, Error = e.Message }; }
                return _graph;
            }
        }
    }
}
