using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Named shader reference with the property-ID registry ported code uses
    /// (`Shader.PropertyToID` for MaterialPropertyBlock-style access). The actual
    /// shading backend arrives in the presentation phase.
    /// </summary>
    public sealed class Shader : Object
    {
        static readonly Dictionary<string, int> PropertyIds = new();
        static readonly Dictionary<string, Shader> Registry = new();

        Shader(string shaderName) { name = shaderName; }

        public static Shader Find(string name)
        {
            if (!Registry.TryGetValue(name, out var shader))
                Registry[name] = shader = new Shader(name);
            return shader;
        }

        /// <summary>Stable per-process numeric ID for a shader property name.</summary>
        public static int PropertyToID(string name)
        {
            if (!PropertyIds.TryGetValue(name, out int id))
                PropertyIds[name] = id = PropertyIds.Count + 1;
            return id;
        }
    }

    /// <summary>
    /// Material as a property store (colors/floats/vectors keyed by shader property),
    /// preserving the API surface gameplay code touches: clone construction, color,
    /// Set/Get by name or ID. Rendering interpretation arrives in the presentation
    /// phase; until then materials are pure data and fully testable.
    /// </summary>
    public class Material : Object
    {
        public Shader shader;

        readonly Dictionary<int, Color> _colors = new();
        readonly Dictionary<int, float> _floats = new();
        readonly Dictionary<int, Vector4> _vectors = new();
        readonly Dictionary<int, int> _ints = new();

        static readonly int ColorId = Shader.PropertyToID("_Color");

        public Material(Shader shader)
        {
            this.shader = shader;
            name = shader is not null ? (string)shader.name : "Material";
        }

        /// <summary>Clone constructor — the `new Material(other)` instancing pattern.</summary>
        public Material(Material source)
        {
            shader = source.shader;
            name = source.name + " (Instance)";
            foreach (var kv in source._colors) _colors[kv.Key] = kv.Value;
            foreach (var kv in source._floats) _floats[kv.Key] = kv.Value;
            foreach (var kv in source._vectors) _vectors[kv.Key] = kv.Value;
            foreach (var kv in source._ints) _ints[kv.Key] = kv.Value;
        }

        public Color color
        {
            get => GetColor(ColorId);
            set => SetColor(ColorId, value);
        }

        public void SetColor(string propertyName, Color value) => _colors[Shader.PropertyToID(propertyName)] = value;
        public void SetColor(int nameID, Color value) => _colors[nameID] = value;
        public Color GetColor(string propertyName) => GetColor(Shader.PropertyToID(propertyName));
        public Color GetColor(int nameID) => _colors.TryGetValue(nameID, out var v) ? v : Color.white;

        public void SetFloat(string propertyName, float value) => _floats[Shader.PropertyToID(propertyName)] = value;
        public void SetFloat(int nameID, float value) => _floats[nameID] = value;
        public float GetFloat(string propertyName) => GetFloat(Shader.PropertyToID(propertyName));
        public float GetFloat(int nameID) => _floats.TryGetValue(nameID, out var v) ? v : 0f;

        public void SetVector(string propertyName, Vector4 value) => _vectors[Shader.PropertyToID(propertyName)] = value;
        public void SetVector(int nameID, Vector4 value) => _vectors[nameID] = value;
        public Vector4 GetVector(string propertyName) => GetVector(Shader.PropertyToID(propertyName));
        public Vector4 GetVector(int nameID) => _vectors.TryGetValue(nameID, out var v) ? v : Vector4.zero;

        public void SetInt(string propertyName, int value) => _ints[Shader.PropertyToID(propertyName)] = value;
        public int GetInt(string propertyName) => _ints.TryGetValue(Shader.PropertyToID(propertyName), out var v) ? v : 0;

        public bool HasProperty(string propertyName) => HasProperty(Shader.PropertyToID(propertyName));
        public bool HasProperty(int nameID)
            => _colors.ContainsKey(nameID) || _floats.ContainsKey(nameID)
            || _vectors.ContainsKey(nameID) || _ints.ContainsKey(nameID);

        /// <summary>
        /// Interpolate this material's properties between <paramref name="start"/> and
        /// <paramref name="end"/> (the original engine's Material.Lerp): every color, float,
        /// and vector property named by either endpoint is set to the blend of the two.
        /// </summary>
        public void Lerp(Material start, Material end, float t)
        {
            if (start is null || end is null) return;
            t = Mathf.Clamp01(t);

            var colorKeys = new HashSet<int>(start._colors.Keys);
            colorKeys.UnionWith(end._colors.Keys);
            foreach (var key in colorKeys)
                _colors[key] = Color.Lerp(start.GetColor(key), end.GetColor(key), t);

            var floatKeys = new HashSet<int>(start._floats.Keys);
            floatKeys.UnionWith(end._floats.Keys);
            foreach (var key in floatKeys)
                _floats[key] = Mathf.Lerp(start.GetFloat(key), end.GetFloat(key), t);

            var vectorKeys = new HashSet<int>(start._vectors.Keys);
            vectorKeys.UnionWith(end._vectors.Keys);
            foreach (var key in vectorKeys)
                _vectors[key] = Vector4.Lerp(start.GetVector(key), end.GetVector(key), t);
        }
    }
}
