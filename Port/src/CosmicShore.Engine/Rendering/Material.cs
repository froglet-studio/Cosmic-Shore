using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Named shader reference with the property-ID registry ported code uses
    /// (`Shader.PropertyToID` for MaterialPropertyBlock-style access). The actual
    /// shading backend arrives in the presentation phase.
    /// </summary>
    public sealed partial class Shader : Object
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
            {
                PropertyIds[name] = id = PropertyIds.Count + 1;
                PropertyNames[id] = name;
            }
            return id;
        }

        static readonly Dictionary<int, string> PropertyNames = new();

        // ── Declared properties (read from the shader source by the content layer) ──

        static System.Func<string, IReadOnlyList<KeyValuePair<string, object>>> s_catalog;
        static int s_catalogGeneration;
        List<KeyValuePair<string, object>> _declaredList;
        Dictionary<int, object> _declared;
        int _declaredGeneration = -1;

        /// <summary>
        /// Port hook: resolves a shader name to its declared properties (name → default value:
        /// float, Color, Vector4, or null for a texture) in declaration order, or null when the
        /// shader's source is not available. Set by the content layer.
        /// </summary>
        public static System.Func<string, IReadOnlyList<KeyValuePair<string, object>>> PropertyCatalog
        {
            get => s_catalog;
            set { s_catalog = value; s_catalogGeneration++; }
        }

        Dictionary<int, object> Declared
        {
            get
            {
                if (_declaredGeneration == s_catalogGeneration) return _declared;
                _declaredGeneration = s_catalogGeneration;
                _declared = null; _declaredList = null;
                var props = s_catalog?.Invoke(name);
                if (props == null) return null;
                _declared = new Dictionary<int, object>();
                _declaredList = new List<KeyValuePair<string, object>>();
                foreach (var kv in props)
                    if (_declared.TryAdd(PropertyToID(kv.Key), kv.Value)) _declaredList.Add(kv);
                return _declared;
            }
        }

        /// <summary>True when this shader's source declares the property.</summary>
        public bool DeclaresProperty(int nameID) => Declared?.ContainsKey(nameID) == true;

        /// <summary>The declared default of a property (float, Color, Vector4; null for textures).</summary>
        public bool TryGetDefault(int nameID, out object value)
        {
            value = null;
            var d = Declared;
            return d != null && d.TryGetValue(nameID, out value) && value != null;
        }

        /// <summary>Index of a declared property, or -1 (original contract).</summary>
        public int FindPropertyIndex(string propertyName)
        {
            _ = Declared;
            if (_declaredList == null) return -1;
            for (int i = 0; i < _declaredList.Count; i++)
                if (_declaredList[i].Key == propertyName) return i;
            return -1;
        }

        /// <summary>Number of declared properties (0 when the shader's source is unknown).</summary>
        public int GetPropertyCount() { _ = Declared; return _declaredList?.Count ?? 0; }

        public string GetPropertyName(int propertyIndex)
        {
            _ = Declared;
            return _declaredList != null && propertyIndex >= 0 && propertyIndex < _declaredList.Count ? _declaredList[propertyIndex].Key : null;
        }

        /// <summary>Reverse lookup of <see cref="PropertyToID"/> (port helper).</summary>
        public static string PropertyName(int id) => PropertyNames.TryGetValue(id, out var n) ? n : $"_Property{id}";
    }

    /// <summary>
    /// Material as a property store (colors/floats/vectors keyed by shader property),
    /// preserving the API surface gameplay code touches: clone construction, color,
    /// Set/Get by name or ID. Rendering interpretation arrives in the presentation
    /// phase; until then materials are pure data and fully testable.
    /// </summary>
    public partial class Material : Object
    {
        Shader _shader;
        public Shader shader { get => _shader; set { _shader = value; Revision++; } }

        /// <summary>Bumped by every mutation, so a renderer caching what it derived from this
        /// material knows when a runtime SetFloat/SetColor/shader swap must be re-read.</summary>
        public int Revision { get; private set; }

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
            foreach (var keyword in source._keywords) _keywords.Add(keyword);
            foreach (var kv in source._textures) _textures[kv.Key] = kv.Value;
            foreach (var kv in source._textureST) _textureST[kv.Key] = kv.Value;
            renderQueue = source.renderQueue;
            Revision++;
        }

        public Color color
        {
            get => GetColor(ColorId);
            set => SetColor(ColorId, value);
        }

        public void SetColor(string propertyName, Color value) { _colors[Shader.PropertyToID(propertyName)] = value; Revision++; }
        public void SetColor(int nameID, Color value) { _colors[nameID] = value; Revision++; }
        public Color GetColor(string propertyName) => GetColor(Shader.PropertyToID(propertyName));
        public Color GetColor(int nameID)
        {
            if (_colors.TryGetValue(nameID, out var v)) return v;
            if (shader is not null && shader.TryGetDefault(nameID, out var d))
                return d switch { Color c => c, Vector4 w => new Color(w.x, w.y, w.z, w.w), float f => new Color(f, f, f, f), _ => Color.white };
            return Color.white;
        }

        public void SetFloat(string propertyName, float value) { _floats[Shader.PropertyToID(propertyName)] = value; Revision++; }
        public void SetFloat(int nameID, float value) { _floats[nameID] = value; Revision++; }
        public float GetFloat(string propertyName) => GetFloat(Shader.PropertyToID(propertyName));
        public float GetFloat(int nameID)
        {
            if (_floats.TryGetValue(nameID, out var v)) return v;
            if (shader is not null && shader.TryGetDefault(nameID, out var d) && d is float f) return f;
            return 0f;
        }

        public void SetVector(string propertyName, Vector4 value) { _vectors[Shader.PropertyToID(propertyName)] = value; Revision++; }
        public void SetVector(int nameID, Vector4 value) { _vectors[nameID] = value; Revision++; }
        public Vector4 GetVector(string propertyName) => GetVector(Shader.PropertyToID(propertyName));
        public Vector4 GetVector(int nameID)
        {
            if (_vectors.TryGetValue(nameID, out var v)) return v;
            if (shader is not null && shader.TryGetDefault(nameID, out var d))
                return d switch { Vector4 w => w, Color c => new Vector4(c.r, c.g, c.b, c.a), _ => Vector4.zero };
            return Vector4.zero;
        }

        readonly Dictionary<int, Texture> _textures = new();
        readonly Dictionary<int, Vector4> _textureST = new();

        /// <summary>Texture slot (Arc E: filled from a .mat's m_TexEnvs by the content bridge).</summary>
        public void SetTexture(string propertyName, Texture value) { _textures[Shader.PropertyToID(propertyName)] = value; Revision++; }
        public void SetTexture(int nameID, Texture value) { _textures[nameID] = value; Revision++; }
        public Texture GetTexture(string propertyName) => GetTexture(Shader.PropertyToID(propertyName));
        public Texture GetTexture(int nameID) => _textures.TryGetValue(nameID, out var t) ? t : null;

        /// <summary>Texture tiling (xy) + offset (zw) for a slot — original _ST convention.</summary>
        public void SetTextureScaleOffset(string propertyName, Vector4 st) { _textureST[Shader.PropertyToID(propertyName)] = st; Revision++; }
        public Vector4 GetTextureScaleOffset(string propertyName)
            => _textureST.TryGetValue(Shader.PropertyToID(propertyName), out var v) ? v : new Vector4(1f, 1f, 0f, 0f);

        public Texture mainTexture
        {
            get => GetTexture("_MainTex") ?? GetTexture("_BaseMap");
            set => SetTexture("_MainTex", value);
        }

        public void SetInt(string propertyName, int value) { _ints[Shader.PropertyToID(propertyName)] = value; Revision++; }
        public int GetInt(string propertyName) => _ints.TryGetValue(Shader.PropertyToID(propertyName), out var v) ? v : 0;

        // ── Keywords + render queue (E18 — data-only, read by a future backend) ──

        readonly HashSet<string> _keywords = new();

        /// <summary>Render queue (original default 2000/Geometry; transparency setup code writes 3000+).</summary>
        int _renderQueue = 2000;
        public int renderQueue { get => _renderQueue; set { _renderQueue = value; Revision++; } }

        public void EnableKeyword(string keyword) { if (_keywords.Add(keyword)) Revision++; }
        public void DisableKeyword(string keyword) { if (_keywords.Remove(keyword)) Revision++; }
        public bool IsKeywordEnabled(string keyword) => _keywords.Contains(keyword);

        public bool HasProperty(string propertyName) => HasProperty(Shader.PropertyToID(propertyName));
        /// <summary>
        /// Original contract: true when the SHADER declares the property, whether or not this
        /// material has a value saved for it (a property added to a graph after the material
        /// was last saved still exists). Falls back to the stored values when the shader's
        /// declarations are unknown.
        /// </summary>
        public bool HasProperty(int nameID)
            => HasStoredProperty(nameID) || (shader is not null && shader.DeclaresProperty(nameID));

        /// <summary>Port helper: true only when this material carries its own value (the renderer's classification reads this).</summary>
        public bool HasStoredProperty(string propertyName) => HasStoredProperty(Shader.PropertyToID(propertyName));
        public bool HasStoredProperty(int nameID)
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
            Revision++;
        }
    }
}
