using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CosmicShore.Content.Shaders
{
    /// <summary>
    /// What a hand-written <c>.shader</c> (ShaderLab) declares outside its HLSL: the Properties
    /// block (name, type, default), the first SubShader's tags and its first pass's fixed-function
    /// state (Blend, ZWrite, Cull), plus whether it is a lit surface shader. The hand translations
    /// (<see cref="HandShaders"/>) take their render state and texture defaults from here, so only
    /// the shader's maths is written by hand.
    /// </summary>
    public sealed class ShaderLabSource
    {
        public sealed record Property(string Name, string Type, string Default);

        public string Name = "";
        public readonly List<Property> Properties = new();
        public readonly Dictionary<string, string> Tags = new(StringComparer.Ordinal);
        /// <summary>Blend factors as written ("SrcAlpha", "One", "[_SrcBlend]"), null when the pass does not blend.</summary>
        public string BlendSrc, BlendDst;
        /// <summary>"On"/"Off"/"[_ZWrite]", or null when the pass leaves the default.</summary>
        public string ZWrite;
        /// <summary>"Off"/"Front"/"Back"/"[_Cull]", or null (Back).</summary>
        public string Cull;
        /// <summary>A <c>#pragma surface</c> shader with a lighting model other than NoLighting.</summary>
        public bool SurfaceLit;
        /// <summary>A <c>#pragma surface ... alpha</c> shader (blends like SrcAlpha OneMinusSrcAlpha).</summary>
        public bool SurfaceAlpha;

        static readonly Regex ShaderName = new(@"Shader\s+""([^""]+)""", RegexOptions.Compiled);
        // [Attr] _Name ("Label", Type) = default   (Type: Color, Vector, Float, Int, Range(a, b), 2D, 3D, Cube)
        static readonly Regex PropertyLine = new(@"^\s*(?:\[[^\]]*\]\s*)*(_?\w+)\s*\(\s*""[^""]*""\s*,\s*(Range\s*\([^)]*\)|\w+)\s*\)\s*=\s*(.+?)\s*(?://.*)?$", RegexOptions.Compiled);
        static readonly Regex Tag = new(@"""([^""]+)""\s*=\s*""([^""]*)""", RegexOptions.Compiled);

        public static ShaderLabSource Parse(string text)
        {
            var src = new ShaderLabSource();
            var m = ShaderName.Match(text);
            if (m.Success) src.Name = m.Groups[1].Value;

            int props = Block(text, "Properties", 0, out int propsEnd);
            if (props >= 0)
                foreach (var line in text.Substring(props, propsEnd - props).Split('\n'))
                {
                    var p = PropertyLine.Match(line);
                    if (!p.Success) continue;
                    string type = p.Groups[2].Value.StartsWith("Range", StringComparison.Ordinal) ? "Range" : p.Groups[2].Value;
                    src.Properties.Add(new Property(p.Groups[1].Value, type, p.Groups[3].Value.Trim()));
                }

            int sub = Block(text, "SubShader", Math.Max(0, propsEnd), out int subEnd);
            if (sub < 0) return src;
            string body = text.Substring(sub, subEnd - sub);
            // Code is not state: drop the program blocks before reading fixed-function lines.
            string state = Regex.Replace(body, @"(CG|HLSL)PROGRAM.*?END(CG|HLSL)", "", RegexOptions.Singleline);
            int tags = Block(state, "Tags", 0, out int tagsEnd);
            if (tags >= 0)
                foreach (Match t in Tag.Matches(state.Substring(tags, tagsEnd - tags))) src.Tags[t.Groups[1].Value] = t.Groups[2].Value;
            var blend = Regex.Match(state, @"^\s*Blend\s+(\S+)(?:\s+(\S+))?", RegexOptions.Multiline);
            if (blend.Success && !blend.Groups[1].Value.Equals("Off", StringComparison.OrdinalIgnoreCase))
            {
                src.BlendSrc = blend.Groups[1].Value;
                src.BlendDst = blend.Groups[2].Success ? blend.Groups[2].Value.TrimEnd(',') : "Zero";
            }
            var zw = Regex.Match(state, @"^\s*ZWrite\s+(\S+)", RegexOptions.Multiline);
            if (zw.Success) src.ZWrite = zw.Groups[1].Value;
            var cull = Regex.Match(state, @"^\s*Cull\s+(\S+)", RegexOptions.Multiline);
            if (cull.Success) src.Cull = cull.Groups[1].Value;
            var surface = Regex.Match(body, @"#pragma\s+surface\s+\w+\s+(\w+)([^\n]*)");
            if (surface.Success)
            {
                src.SurfaceLit = surface.Groups[1].Value != "NoLighting";
                src.SurfaceAlpha = Regex.IsMatch(surface.Groups[2].Value, @"\balpha\b|alpha:");
            }
            return src;
        }

        /// <summary>The render queue the tags ask for ("Transparent+2" is 3002), or -1.</summary>
        public int Queue
        {
            get
            {
                if (!Tags.TryGetValue("Queue", out var q)) return -1;
                var mm = Regex.Match(q, @"^(\w+)\s*([+-]\s*\d+)?$");
                if (!mm.Success) return -1;
                int baseQ = mm.Groups[1].Value switch
                {
                    "Background" => 1000, "Geometry" => 2000, "AlphaTest" => 2450, "Transparent" => 3000, "Overlay" => 4000,
                    _ => int.TryParse(mm.Groups[1].Value, out var n) ? n : -1,
                };
                if (baseQ < 0) return -1;
                return mm.Groups[2].Success ? baseQ + int.Parse(mm.Groups[2].Value.Replace(" ", ""), CultureInfo.InvariantCulture) : baseQ;
            }
        }

        /// <summary>Unity's BlendMode value for a factor name (UnityEngine.Rendering.BlendMode), or -1 for a property reference.</summary>
        public static int BlendFactor(string name) => name switch
        {
            "Zero" => 0, "One" => 1, "DstColor" => 2, "SrcColor" => 3, "OneMinusDstColor" => 4, "SrcAlpha" => 5,
            "OneMinusSrcColor" => 6, "DstAlpha" => 7, "OneMinusDstAlpha" => 8, "SrcAlphaSaturate" => 9, "OneMinusSrcAlpha" => 10,
            _ => -1,
        };

        /// <summary>The <c>{ ... }</c> body after the keyword (start inside the brace, end at its match), or -1.</summary>
        static int Block(string text, string keyword, int from, out int end)
        {
            end = -1;
            var m = new Regex(@"\b" + keyword + @"\s*\{").Match(text, from);
            if (!m.Success) return -1;
            int start = m.Index + m.Length, depth = 1;
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0) { end = i; return start; }
            }
            return -1;
        }
    }
}
