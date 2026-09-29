using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    [Flags]
    public enum MaterialGlobalIlluminationFlags { None = 0, RealtimeEmissive = 1, BakedEmissive = 2, EmissiveIsBlack = 4, AnyEmissive = 3 }

    /// <summary>
    /// Global shader state (original contract: Shader.SetGlobal* / global keywords). The renderer
    /// reads these each frame; scripts publish per-frame globals (occlusion corridor, sight cones,
    /// sway clocks) through them.
    /// </summary>
    public sealed partial class Shader
    {
        public static readonly Dictionary<int, float> GlobalFloats = new();
        public static readonly Dictionary<int, Vector4> GlobalVectors = new();
        public static readonly Dictionary<int, Color> GlobalColors = new();
        public static readonly Dictionary<int, Matrix4x4> GlobalMatrices = new();
        public static readonly Dictionary<int, Texture> GlobalTextures = new();
        public static readonly Dictionary<int, float[]> GlobalFloatArrays = new();
        public static readonly Dictionary<int, Vector4[]> GlobalVectorArrays = new();
        public static readonly HashSet<string> GlobalKeywords = new();

        public static int globalMaximumLOD { get; set; } = int.MaxValue;
        public static string globalRenderPipeline { get; set; } = "UniversalPipeline";
        public int maximumLOD { get; set; } = int.MaxValue;
        public bool isSupported => true;
        public int renderQueue => 2000;
        public int passCount => 1;

        public static void SetGlobalFloat(string name, float value) => GlobalFloats[PropertyToID(name)] = value;
        public static void SetGlobalFloat(int nameID, float value) => GlobalFloats[nameID] = value;
        public static void SetGlobalInt(string name, int value) => GlobalFloats[PropertyToID(name)] = value;
        public static void SetGlobalInt(int nameID, int value) => GlobalFloats[nameID] = value;
        public static void SetGlobalInteger(int nameID, int value) => GlobalFloats[nameID] = value;
        public static void SetGlobalVector(string name, Vector4 value) => GlobalVectors[PropertyToID(name)] = value;
        public static void SetGlobalVector(int nameID, Vector4 value) => GlobalVectors[nameID] = value;
        public static void SetGlobalColor(string name, Color value) => GlobalColors[PropertyToID(name)] = value;
        public static void SetGlobalColor(int nameID, Color value) => GlobalColors[nameID] = value;
        public static void SetGlobalMatrix(string name, Matrix4x4 value) => GlobalMatrices[PropertyToID(name)] = value;
        public static void SetGlobalMatrix(int nameID, Matrix4x4 value) => GlobalMatrices[nameID] = value;
        public static void SetGlobalTexture(string name, Texture value) => GlobalTextures[PropertyToID(name)] = value;
        public static void SetGlobalTexture(int nameID, Texture value) => GlobalTextures[nameID] = value;

        /// <summary>Array globals keep the FIRST length they were set with (original contract: the size is pinned).</summary>
        public static void SetGlobalFloatArray(int nameID, float[] values) => GlobalFloatArrays[nameID] = Pin(GlobalFloatArrays, nameID, values);
        public static void SetGlobalFloatArray(string name, float[] values) => SetGlobalFloatArray(PropertyToID(name), values);
        public static void SetGlobalFloatArray(int nameID, List<float> values) => SetGlobalFloatArray(nameID, values.ToArray());
        public static void SetGlobalVectorArray(int nameID, Vector4[] values) => GlobalVectorArrays[nameID] = Pin(GlobalVectorArrays, nameID, values);
        public static void SetGlobalVectorArray(string name, Vector4[] values) => SetGlobalVectorArray(PropertyToID(name), values);
        public static void SetGlobalVectorArray(int nameID, List<Vector4> values) => SetGlobalVectorArray(nameID, values.ToArray());

        static T[] Pin<T>(Dictionary<int, T[]> store, int id, T[] values)
        {
            if (store.TryGetValue(id, out var existing) && existing.Length != values.Length)
            {
                var pinned = new T[existing.Length];
                Array.Copy(values, pinned, Math.Min(values.Length, pinned.Length));
                return pinned;
            }
            return (T[])values.Clone();
        }

        public static float GetGlobalFloat(string name) => GetGlobalFloat(PropertyToID(name));
        public static float GetGlobalFloat(int nameID) => GlobalFloats.TryGetValue(nameID, out var v) ? v : 0f;
        public static int GetGlobalInt(int nameID) => (int)GetGlobalFloat(nameID);
        public static Vector4 GetGlobalVector(string name) => GetGlobalVector(PropertyToID(name));
        public static Vector4 GetGlobalVector(int nameID) => GlobalVectors.TryGetValue(nameID, out var v) ? v : Vector4.zero;
        public static Color GetGlobalColor(int nameID) => GlobalColors.TryGetValue(nameID, out var v) ? v : Color.clear;
        public static Matrix4x4 GetGlobalMatrix(int nameID) => GlobalMatrices.TryGetValue(nameID, out var v) ? v : Matrix4x4.identity;
        public static Texture GetGlobalTexture(int nameID) => GlobalTextures.TryGetValue(nameID, out var v) ? v : null;
        public static Vector4[] GetGlobalVectorArray(int nameID) => GlobalVectorArrays.TryGetValue(nameID, out var v) ? v : null;
        public static float[] GetGlobalFloatArray(int nameID) => GlobalFloatArrays.TryGetValue(nameID, out var v) ? v : null;

        public static void EnableKeyword(string keyword) => GlobalKeywords.Add(keyword);
        public static void DisableKeyword(string keyword) => GlobalKeywords.Remove(keyword);
        public static bool IsKeywordEnabled(string keyword) => GlobalKeywords.Contains(keyword);
        public static void WarmupAllShaders() { }

        public int FindPropertyIndex(string propertyName) => -1;
        public int GetPropertyCount() => 0;
    }

    public partial class Material
    {
        public MaterialGlobalIlluminationFlags globalIlluminationFlags { get; set; } = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        public bool enableInstancing { get; set; } = true;
        public bool doubleSidedGI { get; set; }
        public int passCount => 1;

        readonly Dictionary<int, Matrix4x4> _matrices = new();
        readonly Dictionary<int, float[]> _floatArrays = new();
        readonly Dictionary<int, Vector4[]> _vectorArrays = new();
        readonly Dictionary<string, string> _tags = new();

        public Material() : this((Shader)null) { }

        public bool HasColor(string name) => HasColor(Shader.PropertyToID(name));
        public bool HasColor(int nameID) => HasProperty(nameID);
        public bool HasFloat(string name) => HasFloat(Shader.PropertyToID(name));
        public bool HasFloat(int nameID) => HasProperty(nameID);
        public bool HasInt(string name) => HasProperty(Shader.PropertyToID(name));
        public bool HasInteger(int nameID) => HasProperty(nameID);
        public bool HasVector(string name) => HasProperty(Shader.PropertyToID(name));
        public bool HasVector(int nameID) => HasProperty(nameID);
        public bool HasTexture(string name) => GetTexture(name) != null;
        public bool HasTexture(int nameID) => GetTexture(nameID) != null;
        public bool HasMatrix(int nameID) => _matrices.ContainsKey(nameID);

        public void SetInt(int nameID, int value) => SetInt(IdName(nameID), value);
        public int GetInt(int nameID) => GetInt(IdName(nameID));
        public void SetInteger(string name, int value) => SetInt(name, value);
        public void SetInteger(int nameID, int value) => SetInt(nameID, value);
        public int GetInteger(string name) => GetInt(name);
        public int GetInteger(int nameID) => GetInt(nameID);

        public void SetMatrix(string name, Matrix4x4 value) => _matrices[Shader.PropertyToID(name)] = value;
        public void SetMatrix(int nameID, Matrix4x4 value) => _matrices[nameID] = value;
        public Matrix4x4 GetMatrix(string name) => GetMatrix(Shader.PropertyToID(name));
        public Matrix4x4 GetMatrix(int nameID) => _matrices.TryGetValue(nameID, out var m) ? m : Matrix4x4.identity;

        public void SetFloatArray(string name, float[] values) => _floatArrays[Shader.PropertyToID(name)] = (float[])values.Clone();
        public void SetFloatArray(int nameID, float[] values) => _floatArrays[nameID] = (float[])values.Clone();
        public void SetVectorArray(string name, Vector4[] values) => _vectorArrays[Shader.PropertyToID(name)] = (Vector4[])values.Clone();
        public void SetVectorArray(int nameID, Vector4[] values) => _vectorArrays[nameID] = (Vector4[])values.Clone();
        public float[] GetFloatArray(int nameID) => _floatArrays.TryGetValue(nameID, out var a) ? a : null;
        public Vector4[] GetVectorArray(int nameID) => _vectorArrays.TryGetValue(nameID, out var a) ? a : null;

        public Vector2 mainTextureOffset
        {
            get { var st = GetTextureScaleOffset("_MainTex"); return new Vector2(st.z, st.w); }
            set { var st = GetTextureScaleOffset("_MainTex"); SetTextureScaleOffset("_MainTex", new Vector4(st.x, st.y, value.x, value.y)); }
        }

        public Vector2 mainTextureScale
        {
            get { var st = GetTextureScaleOffset("_MainTex"); return new Vector2(st.x, st.y); }
            set { var st = GetTextureScaleOffset("_MainTex"); SetTextureScaleOffset("_MainTex", new Vector4(value.x, value.y, st.z, st.w)); }
        }

        public void SetTextureOffset(string name, Vector2 value) { var st = GetTextureScaleOffset(name); SetTextureScaleOffset(name, new Vector4(st.x, st.y, value.x, value.y)); }
        public void SetTextureScale(string name, Vector2 value) { var st = GetTextureScaleOffset(name); SetTextureScaleOffset(name, new Vector4(value.x, value.y, st.z, st.w)); }
        public Vector2 GetTextureOffset(string name) { var st = GetTextureScaleOffset(name); return new Vector2(st.z, st.w); }
        public Vector2 GetTextureScale(string name) { var st = GetTextureScaleOffset(name); return new Vector2(st.x, st.y); }

        public string[] shaderKeywords
        {
            get => new List<string>(_keywords).ToArray();
            set { _keywords.Clear(); if (value != null) foreach (var k in value) _keywords.Add(k); }
        }

        public void SetKeyword(string keyword, bool value) { if (value) EnableKeyword(keyword); else DisableKeyword(keyword); }

        public void SetOverrideTag(string tag, string val) => _tags[tag] = val;
        public string GetTag(string tag, bool searchFallbacks, string defaultValue = "") => _tags.TryGetValue(tag, out var v) ? v : defaultValue;
        public void SetShaderPassEnabled(string passName, bool enabled) { }
        public bool GetShaderPassEnabled(string passName) => true;
        public bool SetPass(int pass) => true;
        public int ComputeCRC() => GetHashCode();

        public void CopyPropertiesFromMaterial(Material mat)
        {
            if (mat is null) return;
            foreach (var kv in mat._colors) _colors[kv.Key] = kv.Value;
            foreach (var kv in mat._floats) _floats[kv.Key] = kv.Value;
            foreach (var kv in mat._vectors) _vectors[kv.Key] = kv.Value;
            foreach (var kv in mat._ints) _ints[kv.Key] = kv.Value;
            foreach (var kv in mat._textures) _textures[kv.Key] = kv.Value;
            foreach (var kv in mat._textureST) _textureST[kv.Key] = kv.Value;
            foreach (var kv in mat._matrices) _matrices[kv.Key] = kv.Value;
        }

        public void CopyMatchingPropertiesFromMaterial(Material mat) => CopyPropertiesFromMaterial(mat);

        public string[] GetTexturePropertyNames() => Array.Empty<string>();
        public int[] GetTexturePropertyNameIDs() => new List<int>(_textures.Keys).ToArray();

        static string IdName(int id) => Shader.PropertyName(id);
    }
}
