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

        public bool isEmpty => _colors.Count == 0 && _floats.Count == 0 && _vectors.Count == 0;

        public void Clear()
        {
            _colors.Clear();
            _floats.Clear();
            _vectors.Clear();
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
        }
    }
}
