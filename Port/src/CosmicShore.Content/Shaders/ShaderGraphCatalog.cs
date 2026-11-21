using System;
using System.Collections.Concurrent;
using System.IO;
using CosmicShore.Engine;

namespace CosmicShore.Content.Shaders
{
    /// <summary>
    /// The project's Shader Graphs, read and compiled on first use: what <see cref="Shader.GraphProgram"/>
    /// answers from. Keyed by the shader asset's guid, so a renamed graph keeps its program. A
    /// graph whose source is not in this build (packaged player data) yields null.
    /// </summary>
    public sealed class ShaderGraphCatalog
    {
        readonly AssetDatabase _db;
        readonly ConcurrentDictionary<string, ShaderGraphAsset> _assets = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<string, ShaderGraphProgram> _programs = new(StringComparer.Ordinal);
        readonly ShaderGraphCompiler _compiler;

        public ShaderGraphCatalog(AssetDatabase db)
        {
            _db = db;
            _compiler = new ShaderGraphCompiler(LoadAsset);
        }

        /// <summary>The graph asset with this guid, or null when it is not a readable .shadergraph/.shadersubgraph.</summary>
        public ShaderGraphAsset LoadAsset(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            return _assets.GetOrAdd(guid, g =>
            {
                var path = _db.PathOf(g);
                if (path == null || !IsGraphPath(path) || !File.Exists(path)) return null;
                try { return ShaderGraphAsset.Load(path, g); }
                catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { return null; }
            });
        }

        /// <summary>The compiled program for a shader asset guid; null when the guid is not a graph.</summary>
        public ShaderGraphProgram For(string guid, string name = null)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            return _programs.GetOrAdd(guid, g =>
            {
                // A hand-written .shader with a hand translation draws through the same path.
                if (HandShaders.Has(g) && _db.PathOf(g) is { } shaderPath && !IsGraphPath(shaderPath))
                    return HandShaders.For(g, shaderPath, _db.ProjectRelative(shaderPath));
                var asset = LoadAsset(g);
                if (asset == null || asset.IsSubGraph) return null;
                var prog = _compiler.Compile(asset, name ?? "Shader Graphs/" + Path.GetFileNameWithoutExtension(asset.Path));
                prog.AssetPath = _db.ProjectRelative(asset.Path);
                return prog;
            });
        }

        /// <summary>The <see cref="Shader.GraphCompiler"/> hook.</summary>
        public ShaderGraphProgram For(Shader shader) => shader?.Guid == null ? null : For(shader.Guid, shader.name);

        public static bool IsGraphPath(string path)
            => path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".shadersubgraph", StringComparison.OrdinalIgnoreCase);
    }
}
