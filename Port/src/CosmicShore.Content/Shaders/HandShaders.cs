using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CosmicShore.Engine;

namespace CosmicShore.Content.Shaders
{
    /// <summary>
    /// Hand translations of the project's hand-written <c>.shader</c> files (C2): GLSL for the
    /// shader's maths only, against the same symbol contract as the Shader Graph compiler
    /// (<c>sg_vertex</c> / <c>sg_surface</c>, the <c>sg_*</c> built-ins), so the renderer draws them
    /// through the compiled-graph path. Everything else comes from the <c>.shader</c> itself
    /// (<see cref="ShaderLabSource"/>): a uniform per property, texture defaults, Blend / ZWrite /
    /// Cull, the queue, lit or not. Keyed by the shader asset's guid; each translation names the
    /// file it was written against, and a translation file is <c>Hand/NAME.glsl</c>, split into the
    /// shared part, <c>// @vertex</c> and <c>// @fragment</c>.
    /// </summary>
    public static class HandShaders
    {
        /// <summary>guid → (embedded translation, the asset it was written against).</summary>
        public static readonly IReadOnlyDictionary<string, (string Glsl, string Path)> ByGuid = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["b55e562cd5045859607550ce7ba8267f"] = ("OmniShepardFresnelShader", "Assets/_Graphics/Materials/Shaders/OmniShepardFresnelShader.shader"),
            ["db3a7d241b308d845a0133c22554acad"] = ("SpreadFresnelShader", "Assets/_Graphics/Materials/Shaders/SpreadFresnelShader.shader"),
            ["554d96e9b67505208a3fbf0acb165350"] = ("OmniCrystalFresnelShader", "Assets/_Graphics/Materials/Shaders/OmniCrystalFresnelShader.shader"),
            ["20424d64830139e4d856f9eff8565409"] = ("TrailViewerShader", "Assets/_Graphics/Materials/Shaders/TrailViewerShader.shader"),
            ["3ed4773a603820f42915cc01f4dd4f76"] = ("TriangleFresnelShader", "Assets/_Graphics/Materials/Shaders/TriangleFresnelShader.shader"),
            ["8efe19062b480f445801d12c6fca1ded"] = ("JetShader", "Assets/_Scripts/Game/Vessel/Animation/JetShader.shader"),
            ["e66cc995d379448caf38b18d87140dde"] = ("ForcefieldCrackleCapsule", "Assets/_Graphics/Materials/Graphs/ForcefieldCrackleCapsule.shader"),
            ["c451cf0afd834429a6f4e6652dad5b50"] = ("ProjectileChargeField", "Assets/_Graphics/Materials/Graphs/ProjectileChargeField.shader"),
        };

        /// <summary>What a translation knowingly leaves out, by translation name (shown as the program's approximations).</summary>
        static readonly Dictionary<string, string> s_approximations = new(StringComparer.Ordinal)
        {
            ["OmniCrystalFresnelShader"] = "CrystalMorph: the mesh's TEXCOORD2/3 morph targets are not in the graph template; drawn unmorphed",
        };

        public static bool Has(string guid) => guid != null && ByGuid.ContainsKey(guid);

        /// <summary>The embedded GLSL of a translation, or null.</summary>
        public static string Glsl(string name)
        {
            using var s = typeof(HandShaders).Assembly.GetManifestResourceStream("CosmicShore.Content.Shaders.Hand." + name + ".glsl");
            if (s == null) return null;
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }

        /// <summary>
        /// The program for the hand-written shader at <paramref name="shaderPath"/> (guid
        /// <paramref name="guid"/>), or null when there is no translation or no source to read.
        /// </summary>
        public static ShaderGraphProgram For(string guid, string shaderPath, string projectRelative)
        {
            if (!ByGuid.TryGetValue(guid ?? "", out var entry) || shaderPath == null || !File.Exists(shaderPath)) return null;
            var lab = ShaderLabSource.Parse(File.ReadAllText(shaderPath));
            var prog = new ShaderGraphProgram { Guid = guid, AssetPath = projectRelative, Name = lab.Name };
            var glsl = Glsl(entry.Glsl);
            if (glsl == null) { prog.Error = $"Hand/{entry.Glsl}.glsl is not embedded"; return prog; }
            int v = glsl.IndexOf("// @vertex", StringComparison.Ordinal), f = glsl.IndexOf("// @fragment", StringComparison.Ordinal);
            if (v < 0 || f < v) { prog.Error = $"Hand/{entry.Glsl}.glsl needs a // @vertex and a // @fragment section"; return prog; }
            prog.Functions = glsl[..v];
            prog.Vertex = glsl[v..f];
            prog.Fragment = glsl[f..];
            prog.Uniforms = Uniforms(lab, prog);
            ApplyState(lab, prog);
            if (s_approximations.TryGetValue(entry.Glsl, out var approx)) prog.Approximations.Add(approx);
            return prog;
        }

        /// <summary>A uniform per property, named as the property (so materials bind by name), with _ST for textures.</summary>
        static string Uniforms(ShaderLabSource lab, ShaderGraphProgram prog)
        {
            var sb = new StringBuilder();
            foreach (var p in lab.Properties)
            {
                switch (p.Type)
                {
                    case "Color":
                    case "Vector":
                        sb.Append("uniform vec4 ").Append(p.Name).Append(";\n");
                        break;
                    case "Float":
                    case "Range":
                    case "Int":
                    case "Integer":
                        sb.Append("uniform float ").Append(p.Name).Append(";\n");
                        break;
                    case "2D":
                        sb.Append("uniform sampler2D ").Append(p.Name).Append(";\n");
                        sb.Append("uniform vec4 ").Append(p.Name).Append("_ST;\n");
                        prog.PropertyUniforms[p.Name + "_ST"] = p.Name + "_ST";
                        // = "white" {}  → white; ShaderLab's defaults are white, black, gray/grey, bump, red.
                        var d = p.Default.Trim().Trim('{', '}', ' ').Trim('"').ToLowerInvariant();
                        int q = d.IndexOf('"');
                        if (q >= 0) d = d[..q];
                        prog.TextureDefaults[p.Name] = d switch { "black" => "black", "gray" or "grey" => "grey", "bump" => "bump", _ => "white" };
                        break;
                }
                prog.PropertyUniforms.TryAdd(p.Name, p.Name);
            }
            return sb.ToString();
        }

        static void ApplyState(ShaderLabSource lab, ShaderGraphProgram prog)
        {
            prog.Lit = lab.SurfaceLit;
            string src = lab.BlendSrc, dst = lab.BlendDst;
            if (src == null && lab.SurfaceAlpha) (src, dst) = ("SrcAlpha", "OneMinusSrcAlpha");
            int s = src == null ? -1 : ShaderLabSource.BlendFactor(src), d = dst == null ? -1 : ShaderLabSource.BlendFactor(dst);
            if (src != null && (s < 0 || d < 0)) prog.Approximations.Add($"Blend {src} {dst}: a property-driven blend draws as SrcAlpha OneMinusSrcAlpha");
            bool blends = src != null && !(s == 1 && d == 0);
            prog.Transparent = blends;
            prog.BlendSrc = s >= 0 ? s : blends ? 5 : -1;
            prog.BlendDst = d >= 0 ? d : blends ? 10 : -1;
            prog.AlphaMode = (prog.BlendSrc, prog.BlendDst) switch { (1, 10) => 1, (5, 1) or (1, 1) => 2, (2, 0) => 3, _ => 0 };
            prog.ZWrite = lab.ZWrite switch { "On" => 1, "Off" => 0, _ => -1 };
            prog.Cull = lab.Cull switch { "Off" => 0, "Front" => 1, _ => 2 };
            prog.Queue = lab.Queue;
            foreach (var (what, value) in new[] { ("ZWrite", lab.ZWrite), ("Cull", lab.Cull) })
                if (value != null && value.StartsWith("[", StringComparison.Ordinal))
                    prog.Approximations.Add($"{what} {value}: a property-driven value draws as the default");
        }
    }
}
