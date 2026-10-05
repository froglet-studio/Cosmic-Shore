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
        readonly ConcurrentDictionary<string, IReadOnlyList<KeyValuePair<string, object>>> _cache = new(StringComparer.Ordinal);

        public ShaderPropertyCatalog(AssetDatabase db) { _db = db; }

        /// <summary>Declared properties of the named shader, or null when its source is not in the project.</summary>
        public IReadOnlyList<KeyValuePair<string, object>> For(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return null;
            return _cache.GetOrAdd(shaderName, n =>
            {
                var path = PathFor(n);
                if (path == null) return null;
                try
                {
                    var text = File.ReadAllText(path);
                    return path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) ? ParseGraph(text) : ParseShader(text);
                }
                catch (IOException) { return null; }
            });
        }

        string PathFor(string shaderName)
        {
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
        public static IReadOnlyList<KeyValuePair<string, object>> ParseGraph(string text)
        {
            var result = new List<KeyValuePair<string, object>>();
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, AllowMultipleValues = true });
            while (true)
            {
                JsonDocument doc;
                try
                {
                    if (!reader.Read()) break;
                    if (reader.TokenType != JsonTokenType.StartObject) continue;
                    doc = JsonDocument.ParseValue(ref reader);
                }
                catch (JsonException) { break; }
                using (doc)
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("m_Type", out var type) || type.ValueKind != JsonValueKind.String) continue;
                    var t = type.GetString();
                    if (t == null || !t.EndsWith("ShaderProperty", StringComparison.Ordinal)) continue;
                    string reference = Str(root, "m_OverrideReferenceName");
                    if (string.IsNullOrEmpty(reference)) reference = Str(root, "m_DefaultReferenceName");
                    if (string.IsNullOrEmpty(reference)) continue;
                    object value = root.TryGetProperty("m_Value", out var v) ? GraphValue(v, t) : null;
                    result.Add(new KeyValuePair<string, object>(reference, value));
                }
            }
            return result;
        }

        static string Str(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

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
            @"^\s*(?:\[[^\]]*\]\s*)*(?<name>\w+)\s*\(\s*""[^""]*""\s*,\s*(?<type>\w+(?:\s*\([^)]*\))?)\s*\)\s*=\s*(?<value>.+?)\s*$",
            RegexOptions.Compiled);

        /// <summary>A hand-written .shader's Properties block.</summary>
        public static IReadOnlyList<KeyValuePair<string, object>> ParseShader(string text)
        {
            var result = new List<KeyValuePair<string, object>>();
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
                result.Add(new KeyValuePair<string, object>(m.Groups["name"].Value, ShaderValue(m.Groups["type"].Value, m.Groups["value"].Value)));
            }
            return result;
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
