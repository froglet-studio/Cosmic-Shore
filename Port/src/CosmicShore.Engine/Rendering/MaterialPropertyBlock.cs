using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Per-renderer material property overrides (colors/floats/vectors keyed by shader
    /// property ID), preserving the original engine's copy semantics:
    /// <see cref="Renderer.SetPropertyBlock"/> snapshots the block's contents onto the
    /// renderer and <see cref="Renderer.GetPropertyBlock"/> overwrites the destination
    /// with the renderer's current snapshot. Pure data until the presentation phase
    /// interprets it (same contract as <see cref="Material"/>).
    /// </summary>
    public sealed class MaterialPropertyBlock
    {
        readonly Dictionary<int, Color> _colors = new();
        readonly Dictionary<int, float> _floats = new();
        readonly Dictionary<int, Vector4> _vectors = new();

        readonly Dictionary<int, Texture> _textures = new();
        readonly Dictionary<int, Matrix4x4> _matrices = new();
        readonly Dictionary<int, float[]> _floatArrays = new();
        readonly Dictionary<int, Vector4[]> _vectorArrays = new();

        public bool isEmpty => _colors.Count == 0 && _floats.Count == 0 && _vectors.Count == 0
                               && _textures.Count == 0 && _matrices.Count == 0 && _floatArrays.Count == 0 && _vectorArrays.Count == 0;

        public void Clear()
        {
            _colors.Clear();
            _floats.Clear();
            _vectors.Clear();
            _textures.Clear();
            _matrices.Clear();
            _floatArrays.Clear();
            _vectorArrays.Clear();
        }

        public void Clear(bool includeTextures) => Clear();

        public void SetInt(string name, int value) => SetFloat(name, value);
        public void SetInt(int nameID, int value) => SetFloat(nameID, value);
        public void SetInteger(string name, int value) => SetFloat(name, value);
        public void SetInteger(int nameID, int value) => SetFloat(nameID, value);
        public int GetInt(string name) => (int)GetFloat(name);
        public int GetInt(int nameID) => (int)GetFloat(nameID);
        public int GetInteger(int nameID) => (int)GetFloat(nameID);

        public void SetTexture(string name, Texture value) => _textures[Shader.PropertyToID(name)] = value;
        public void SetTexture(int nameID, Texture value) => _textures[nameID] = value;
        public Texture GetTexture(string name) => GetTexture(Shader.PropertyToID(name));
        public Texture GetTexture(int nameID) => _textures.TryGetValue(nameID, out var t) ? t : null;

        public void SetMatrix(string name, Matrix4x4 value) => _matrices[Shader.PropertyToID(name)] = value;
        public void SetMatrix(int nameID, Matrix4x4 value) => _matrices[nameID] = value;
        public Matrix4x4 GetMatrix(int nameID) => _matrices.TryGetValue(nameID, out var m) ? m : Matrix4x4.identity;

        public void SetFloatArray(string name, float[] values) => _floatArrays[Shader.PropertyToID(name)] = (float[])values.Clone();
        public void SetFloatArray(int nameID, float[] values) => _floatArrays[nameID] = (float[])values.Clone();
        public void SetFloatArray(int nameID, List<float> values) => _floatArrays[nameID] = values.ToArray();
        public void SetVectorArray(string name, Vector4[] values) => _vectorArrays[Shader.PropertyToID(name)] = (Vector4[])values.Clone();
        public void SetVectorArray(int nameID, Vector4[] values) => _vectorArrays[nameID] = (Vector4[])values.Clone();
        public void SetVectorArray(int nameID, List<Vector4> values) => _vectorArrays[nameID] = values.ToArray();
        public float[] GetFloatArray(int nameID) => _floatArrays.TryGetValue(nameID, out var a) ? a : null;
        public Vector4[] GetVectorArray(int nameID) => _vectorArrays.TryGetValue(nameID, out var a) ? a : null;

        public bool HasFloat(int nameID) => _floats.ContainsKey(nameID);
        public bool HasColor(int nameID) => _colors.ContainsKey(nameID);
        public bool HasVector(int nameID) => _vectors.ContainsKey(nameID);
        public bool HasTexture(int nameID) => _textures.ContainsKey(nameID);
        public bool HasProperty(int nameID) => HasFloat(nameID) || HasColor(nameID) || HasVector(nameID) || HasTexture(nameID) || _matrices.ContainsKey(nameID);
        public bool HasProperty(string name) => HasProperty(Shader.PropertyToID(name));

        /// <summary>Every override, for the renderer: (id, value) with value a float, Color, Vector4, Texture, Matrix4x4 or array.</summary>
        public IEnumerable<KeyValuePair<int, object>> Enumerate()
        {
            foreach (var kv in _floats) yield return new(kv.Key, kv.Value);
            foreach (var kv in _colors) yield return new(kv.Key, kv.Value);
            foreach (var kv in _vectors) yield return new(kv.Key, kv.Value);
            foreach (var kv in _textures) yield return new(kv.Key, kv.Value);
            foreach (var kv in _matrices) yield return new(kv.Key, kv.Value);
            foreach (var kv in _floatArrays) yield return new(kv.Key, kv.Value);
            foreach (var kv in _vectorArrays) yield return new(kv.Key, kv.Value);
        }

        public void SetColor(string name, Color value) => _colors[Shader.PropertyToID(name)] = value;
        public void SetColor(int nameID, Color value) => _colors[nameID] = value;
        public Color GetColor(string name) => GetColor(Shader.PropertyToID(name));
        public Color GetColor(int nameID) => _colors.TryGetValue(nameID, out var v) ? v : Color.clear;

        public void SetFloat(string name, float value) => _floats[Shader.PropertyToID(name)] = value;
        public void SetFloat(int nameID, float value) => _floats[nameID] = value;
        public float GetFloat(string name) => GetFloat(Shader.PropertyToID(name));
        public float GetFloat(int nameID) => _floats.TryGetValue(nameID, out var v) ? v : 0f;

        public void SetVector(string name, Vector4 value) => _vectors[Shader.PropertyToID(name)] = value;
        public void SetVector(int nameID, Vector4 value) => _vectors[nameID] = value;
        public Vector4 GetVector(string name) => GetVector(Shader.PropertyToID(name));
        public Vector4 GetVector(int nameID) => _vectors.TryGetValue(nameID, out var v) ? v : Vector4.zero;

        /// <summary>Overwrite this block's contents with <paramref name="source"/>'s (cleared if null).</summary>
        internal void CopyFrom(MaterialPropertyBlock source)
        {
            Clear();
            if (source == null) return;
            foreach (var kv in source._colors) _colors[kv.Key] = kv.Value;
            foreach (var kv in source._floats) _floats[kv.Key] = kv.Value;
            foreach (var kv in source._vectors) _vectors[kv.Key] = kv.Value;
            foreach (var kv in source._textures) _textures[kv.Key] = kv.Value;
            foreach (var kv in source._matrices) _matrices[kv.Key] = kv.Value;
            foreach (var kv in source._floatArrays) _floatArrays[kv.Key] = kv.Value;
            foreach (var kv in source._vectorArrays) _vectorArrays[kv.Key] = kv.Value;
        }
    }
}
