using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CosmicShore.Engine;

namespace CosmicShore.Content.Shaders
{
    /// <summary>
    /// The properties each project shader DECLARES, read from its source: a Shader Graph's
    /// blackboard (a property's reference name is its override if set, else its default
    /// reference name) or a hand-written .shader's <c>Properties { }</c> block. This is what
    /// the original engine answers <c>Material.HasProperty</c> from — a material saved before
    /// its graph gained a property still "has" it — and where an unset property's value comes
    /// from. Shader names map the way <see cref="MaterialImporter.ShaderNameFor"/> names them.
    /// </summary>
    public sealed class ShaderPropertyCatalog
    {
        readonly AssetDatabase _db;
        Dictionary<string, string> _nameToPath;
        readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.Ordinal);

        sealed class Entry
        {
            public IReadOnlyList<KeyValuePair<string, object>> Properties;
            public IReadOnlyCollection<string> Hdr;
            public ShaderSourceInfo Info;
        }

        public ShaderPropertyCatalog(AssetDatabase db) { _db = db; }

        /// <summary>Declared properties of the named shader, or null when its source is not in the project.</summary>
        public IReadOnlyList<KeyValuePair<string, object>> For(string shaderName) => EntryFor(shaderName)?.Properties;

        /// <summary>
        /// The named shader's [HDR] colour properties (a graph colour in HDR mode, a ShaderLab
        /// property tagged [HDR]), whose stored values are linear; null when its source is unknown.
        /// </summary>
        public IReadOnlyCollection<string> HdrColors(string shaderName) => EntryFor(shaderName)?.Hdr;

        /// <summary>
        /// The named shader's main colour and texture ([MainColor] / [MainTexture]) and its queue
        /// tag; null when its source is unknown.
        /// </summary>
        public ShaderSourceInfo SourceInfo(string shaderName) => EntryFor(shaderName)?.Info;

        Entry EntryFor(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return null;
            return _cache.GetOrAdd(shaderName, n =>
            {
                if (BuiltIn.TryGetValue(n, out var table)) return ParseTable(table);
                var path = PathFor(n);
                if (path == null) return null;
                try
                {
                    var text = File.ReadAllText(path);
                    var hdr = new List<string>();
                    var info = new ShaderSourceInfo();
                    var props = path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) ? ParseGraph(text, hdr, info) : ParseShader(text, hdr, info);
                    return new Entry { Properties = props, Hdr = hdr, Info = info };
                }
                catch (IOException) { return null; }
            });
        }

        /// <summary>
        /// Shaders that ship in packages or with the editor (no source in the project), as their
        /// Properties blocks declare them: K:name=default, K = C colour, H [HDR] colour, V vector,
        /// F float, T texture; MC: / MT: the [MainColor] / [MainTexture] property, Q: the queue tag.
        /// Only what materials and scripts read; enough for HasProperty, defaults, colour space,
        /// Material.color and renderQueue.
        /// </summary>
        static readonly Dictionary<string, string> BuiltIn = new(StringComparer.Ordinal)
        {
            // Unity's built-in shaders (the editor's DefaultResourcesExtra), by the names the
            // project's materials and scripts use.
            ["Particles/Standard Unlit"] = "T:_MainTex C:_Color=1,1,1,1 F:_Cutoff=0.5 F:_BumpScale=1 T:_BumpMap C:_EmissionColor=0,0,0,1 T:_EmissionMap F:_DistortionStrength=1 F:_DistortionBlend=0.5 F:_SoftParticlesNearFadeDistance=0 F:_SoftParticlesFarFadeDistance=1 F:_CameraNearFadeDistance=1 F:_CameraFarFadeDistance=2 F:_Mode=0 F:_ColorMode=0 F:_FlipbookMode=0 F:_LightingEnabled=0 F:_DistortionEnabled=0 F:_EmissionEnabled=0 F:_BlendOp=0 F:_SrcBlend=1 F:_DstBlend=0 F:_ZWrite=1 F:_Cull=2 F:_SoftParticlesEnabled=0 F:_CameraFadingEnabled=0 V:_SoftParticleFadeParams=0,0,0,0 V:_CameraFadeParams=0,0,0,0 V:_ColorAddSubDiff=0,0,0,0 F:_DistortionStrengthScaled=0",
            ["Legacy Shaders/Particles/Alpha Blended"] = "C:_TintColor=0.5,0.5,0.5,0.5 T:_MainTex F:_InvFade=1 Q:3000",
            ["Legacy Shaders/Particles/Additive"] = "C:_TintColor=0.5,0.5,0.5,0.5 T:_MainTex F:_InvFade=1 Q:3000",
            ["Legacy Shaders/Particles/Alpha Blended Premultiply"] = "T:_MainTex F:_InvFade=1 Q:3000",
            ["Sprites/Default"] = "T:_MainTex C:_Color=1,1,1,1 F:PixelSnap=0 C:_RendererColor=1,1,1,1 V:_Flip=1,1,1,1 T:_AlphaTex F:_EnableExternalAlpha=0 Q:3000",
            ["Universal Render Pipeline/Lit"] = "MC:_BaseColor MT:_BaseMap F:_WorkflowMode=1 T:_BaseMap C:_BaseColor=1,1,1,1 F:_Cutoff=0.5 F:_Smoothness=0.5 F:_Metallic=0 T:_MetallicGlossMap C:_SpecColor=0.2,0.2,0.2,1 T:_SpecGlossMap F:_SpecularHighlights=1 F:_EnvironmentReflections=1 F:_BumpScale=1 T:_BumpMap F:_OcclusionStrength=1 T:_OcclusionMap H:_EmissionColor=0,0,0,1 T:_EmissionMap F:_Surface=0 F:_Blend=0 F:_Cull=2 F:_AlphaClip=0 F:_SrcBlend=1 F:_DstBlend=0 F:_ZWrite=1 F:_QueueOffset=0 T:_MainTex C:_Color=1,1,1,1",
            ["Universal Render Pipeline/Unlit"] = "MC:_BaseColor MT:_BaseMap T:_BaseMap C:_BaseColor=1,1,1,1 F:_Cutoff=0.5 F:_Surface=0 F:_Blend=0 F:_Cull=2 F:_AlphaClip=0 F:_SrcBlend=1 F:_DstBlend=0 F:_ZWrite=1 F:_QueueOffset=0 T:_MainTex C:_Color=0.5,0.5,0.5,1",
            ["Universal Render Pipeline/Simple Lit"] = "MC:_BaseColor MT:_BaseMap T:_BaseMap C:_BaseColor=1,1,1,1 F:_Cutoff=0.5 F:_Smoothness=0.5 C:_SpecColor=0.5,0.5,0.5,0.5 T:_SpecGlossMap F:_SpecularHighlights=1 F:_BumpScale=1 T:_BumpMap H:_EmissionColor=0,0,0,1 T:_EmissionMap F:_Surface=0 F:_Blend=0 F:_Cull=2 F:_AlphaClip=0 F:_SrcBlend=1 F:_DstBlend=0 F:_ZWrite=1 F:_QueueOffset=0 T:_MainTex C:_Color=1,1,1,1",
            ["Universal Render Pipeline/Particles/Unlit"] = "MC:_BaseColor MT:_BaseMap T:_BaseMap C:_BaseColor=1,1,1,1 F:_Cutoff=0.5 T:_BumpMap H:_EmissionColor=0,0,0,1 T:_EmissionMap F:_Surface=0 F:_Blend=0 F:_Cull=2 F:_AlphaClip=0 F:_SrcBlend=1 F:_DstBlend=0 F:_ZWrite=1 F:_ColorMode=0 V:_BaseColorAddSubDiff=0,0,0,0 F:_QueueOffset=0 C:_Color=1,1,1,1",
            ["Universal Render Pipeline/Particles/Lit"] = "MC:_BaseColor MT:_BaseMap T:_BaseMap C:_BaseColor=1,1,1,1 F:_Cutoff=0.5 T:_MetallicGlossMap F:_Metallic=0 F:_Smoothness=0.5 F:_BumpScale=1 T:_BumpMap H:_EmissionColor=0,0,0,1 T:_EmissionMap F:_Surface=0 F:_Blend=0 F:_Cull=2 F:_AlphaClip=0 F:_SrcBlend=1 F:_DstBlend=0 F:_ZWrite=1 F:_ColorMode=0 V:_BaseColorAddSubDiff=0,0,0,0 F:_QueueOffset=0 C:_Color=1,1,1,1",
            ["Universal Render Pipeline/Particles/Simple Lit"] = "MC:_BaseColor MT:_BaseMap T:_BaseMap C:_BaseColor=1,1,1,1 F:_Cutoff=0.5 T:_SpecGlossMap C:_SpecColor=1,1,1,1 F:_Smoothness=0.5 F:_BumpScale=1 T:_BumpMap H:_EmissionColor=0,0,0,1 T:_EmissionMap F:_Surface=0 F:_Blend=0 F:_Cull=2 F:_AlphaClip=0 F:_SrcBlend=1 F:_DstBlend=0 F:_ZWrite=1 F:_ColorMode=0 V:_BaseColorAddSubDiff=0,0,0,0 F:_QueueOffset=0 C:_Color=1,1,1,1",
        };

        static Entry ParseTable(string table)
        {
            var props = new List<KeyValuePair<string, object>>();
            var hdr = new List<string>();
            var info = new ShaderSourceInfo();
            foreach (var item in table.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (item.StartsWith("MC:", StringComparison.Ordinal)) { info.MainColor = item.Substring(3); continue; }
                if (item.StartsWith("MT:", StringComparison.Ordinal)) { info.MainTexture = item.Substring(3); continue; }
                if (item.StartsWith("Q:", StringComparison.Ordinal)) { info.Queue = int.Parse(item.Substring(2), CultureInfo.InvariantCulture); continue; }
                char kind = item[0];
                var body = item.Substring(2);
                int eq = body.IndexOf('=');
                var name = eq < 0 ? body : body.Substring(0, eq);
                var value = eq < 0 ? "" : body.Substring(eq + 1);
                float[] Parts() => Array.ConvertAll(value.Split(','), s => float.Parse(s, CultureInfo.InvariantCulture));
                object v = kind switch
                {
                    'C' or 'H' => Parts() is var c ? new Color(c[0], c[1], c[2], c[3]) : null,
                    'V' => Parts() is var w ? new Vector4(w[0], w[1], w[2], w[3]) : null,
                    'F' => float.Parse(value, CultureInfo.InvariantCulture),
                    _ => null,
                };
                if (kind == 'H') hdr.Add(name);
                props.Add(new KeyValuePair<string, object>(name, v));
            }
            return new Entry { Properties = props, Hdr = hdr, Info = info };
        }

        string PathFor(string shaderName)
        {
            if (_db == null) return null; // a catalogue of the built-in table only
            if (_nameToPath == null)
            {
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var path in _db.AllAssetPaths)
                {
                    bool graph = path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase);
                    if (!graph && !path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)) continue;
                    var guid = _db.GuidOf(path);
                    if (guid == null) continue;
                    var name = MaterialImporter.ShaderNameFor(new ObjRef(4800000, guid, 3), _db);
                    map.TryAdd(name, path);
                }
                _nameToPath = map;
            }
            return _nameToPath.TryGetValue(shaderName, out var p) ? p : null;
        }

        /// <summary>Shader Graph v2+: a stream of JSON objects; properties are the *ShaderProperty entries.</summary>
        public static IReadOnlyList<KeyValuePair<string, object>> ParseGraph(string text) => ParseGraph(text, null);

        /// <summary>
        /// <see cref="ParseGraph(string)"/>, adding each HDR-mode colour's reference to <paramref name="hdr"/>
        /// and the main colour / texture to <paramref name="info"/>.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, object>> ParseGraph(string text, List<string> hdr, ShaderSourceInfo info = null)
        {
            var result = new List<KeyValuePair<string, object>>();
            ShaderGraphAsset graph;
            try { graph = ShaderGraphAsset.Parse(text); }
            catch (InvalidDataException) { return result; } // a v1 graph (no GraphData object) declares nothing we read
            foreach (var p in graph.Properties)
            {
                if (string.IsNullOrEmpty(p.Reference)) continue;
                object value = p.Value.ValueKind != JsonValueKind.Undefined ? GraphValue(p.Value, p.Type) : null;
                result.Add(new KeyValuePair<string, object>(p.Reference, value));
                if (hdr != null && p.Type == "ColorShaderProperty" && p.ColorMode == 1) hdr.Add(p.Reference);
                if (info != null && p.IsMainColor) info.MainColor = p.Reference;
                if (info != null && p.IsMainTexture) info.MainTexture = p.Reference;
            }
            return result;
        }

        static object GraphValue(JsonElement v, string type)
        {
            switch (v.ValueKind)
            {
                case JsonValueKind.Number: return (float)v.GetDouble();
                case JsonValueKind.True: return 1f;
                case JsonValueKind.False: return 0f;
                case JsonValueKind.Object:
                    if (v.TryGetProperty("r", out _))
                        return new Color(F(v, "r"), F(v, "g"), F(v, "b"), F(v, "a", 1f));
                    if (v.TryGetProperty("x", out _))
                        return new Vector4(F(v, "x"), F(v, "y"), F(v, "z"), F(v, "w"));
                    return null; // texture / sampler state
                default: return null;
            }
        }

        static float F(JsonElement e, string name, float fallback = 0f)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : fallback;

        static readonly Regex s_propLine = new(
            @"^\s*(?<attrs>(?:\[[^\]]*\]\s*)*)(?<name>\w+)\s*\(\s*""[^""]*""\s*,\s*(?<type>\w+(?:\s*\([^)]*\))?)\s*\)\s*=\s*(?<value>.+?)\s*$",
            RegexOptions.Compiled);

        /// <summary>A hand-written .shader's Properties block.</summary>
        public static IReadOnlyList<KeyValuePair<string, object>> ParseShader(string text) => ParseShader(text, null);

        /// <summary>
        /// <see cref="ParseShader(string)"/>, adding each [HDR] colour's name to <paramref name="hdr"/> and
        /// the [MainColor] / [MainTexture] properties and the first SubShader's queue tag to <paramref name="info"/>.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, object>> ParseShader(string text, List<string> hdr, ShaderSourceInfo info = null)
        {
            var result = new List<KeyValuePair<string, object>>();
            if (info != null && s_queueTag.Match(text) is { Success: true } q) info.Queue = QueueValue(q.Groups["name"].Value, q.Groups["offset"].Value);
            int at = Regex.Match(text, @"\bProperties\s*\{").Index;
            if (at <= 0 && !text.Contains("Properties")) return result;
            int open = text.IndexOf('{', at);
            if (open < 0) return result;
            int depth = 0, end = open;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0) { end = i; break; }
            }
            foreach (var raw in text.Substring(open + 1, end - open - 1).Split('\n'))
            {
                var line = raw;
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment);
                var m = s_propLine.Match(line);
                if (!m.Success) continue;
                var value = ShaderValue(m.Groups["type"].Value, m.Groups["value"].Value);
                result.Add(new KeyValuePair<string, object>(m.Groups["name"].Value, value));
                if (hdr != null && value is Color && Regex.IsMatch(m.Groups["attrs"].Value, @"\[\s*HDR\s*\]", RegexOptions.IgnoreCase))
                    hdr.Add(m.Groups["name"].Value);
                if (info != null && Regex.IsMatch(m.Groups["attrs"].Value, @"\[\s*MainColor\s*\]")) info.MainColor = m.Groups["name"].Value;
                if (info != null && Regex.IsMatch(m.Groups["attrs"].Value, @"\[\s*MainTexture\s*\]")) info.MainTexture = m.Groups["name"].Value;
            }
            return result;
        }

        static readonly Regex s_queueTag = new(@"""Queue""\s*=\s*""(?<name>[A-Za-z]+)(?<offset>[+-]\s*\d+)?""", RegexOptions.Compiled);

        /// <summary>A ShaderLab queue tag as a number: "Transparent+1" = 3001.</summary>
        public static int QueueValue(string name, string offset)
        {
            int baseQueue = name.ToLowerInvariant() switch
            {
                "background" => 1000, "geometry" => 2000, "alphatest" => 2450, "geometrylast" => 2500,
                "transparent" => 3000, "overlay" => 4000, _ => 2000,
            };
            return baseQueue + (string.IsNullOrEmpty(offset) ? 0 : int.Parse(offset.Replace(" ", ""), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
        }

        static object ShaderValue(string type, string value)
        {
            value = value.Trim();
            if (value.StartsWith("\"", StringComparison.Ordinal)) return null; // texture
            if (value.StartsWith("(", StringComparison.Ordinal))
            {
                var parts = value.Trim('(', ')', ' ').Split(',');
                float P(int i) => i < parts.Length && float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
                return type.StartsWith("Color", StringComparison.OrdinalIgnoreCase)
                    ? new Color(P(0), P(1), P(2), parts.Length > 3 ? P(3) : 1f)
                    : new Vector4(P(0), P(1), P(2), P(3));
            }
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
        }
    }
}
