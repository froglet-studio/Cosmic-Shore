using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Content.Editing
{
    /// <summary>
    /// Writes a script's serialized fields the way Unity's serializer would for a fresh
    /// instance: which fields are serialized, in what order, under what names, and in which
    /// YAML shape (Vector3 is a flow map, a Rect is a block with serializedVersion, an int
    /// array is a hex blob …). It is the writing twin of <c>SerializedReader</c>.
    ///
    /// <para>The rules mirror Unity's: fields of the class and its bases, base first, in
    /// declaration order; public or <c>[SerializeField]</c> (including the backing field of a
    /// <c>[field: SerializeField]</c> auto-property, under its compiler name); never static,
    /// const, readonly or <c>[NonSerialized]</c>; only types the serializer supports. A null
    /// string, list, array or <c>[Serializable]</c> class is written as its empty/default
    /// value, because Unity never stores a null there. Nesting stops at depth 10.</para>
    ///
    /// Fidelity is measured, not assumed: <c>cs-asset schema</c> regenerates every script
    /// component in the project from its C# type and compares the field layout with what
    /// Unity actually wrote.
    /// </summary>
    public static class ComponentSerializer
    {
        const int MaxDepth = 10;

        /// <summary>
        /// The full body of a new MonoBehaviour: Unity's header fields, then the script's
        /// serialized fields at the values a fresh instance holds.
        /// </summary>
        public static YMap MonoBehaviourBody(Type type, long gameObjectId, string scriptGuid, string editorClassIdentifier)
        {
            var body = new YMap();
            body.Add("m_ObjectHideFlags", S("0"));
            body.Add("m_CorrespondingSourceObject", YMap.Ref(0));
            body.Add("m_PrefabInstance", YMap.Ref(0));
            body.Add("m_PrefabAsset", YMap.Ref(0));
            body.Add("m_GameObject", YMap.Ref(gameObjectId));
            body.Add("m_Enabled", S("1"));
            body.Add("m_EditorHideFlags", S("0"));
            body.Add("m_Script", YMap.Ref(11500000, scriptGuid, 3));
            body.Add("m_Name", S(""));
            body.Add("m_EditorClassIdentifier", S(editorClassIdentifier ?? ""));
            foreach (var kv in Fields(type, NewInstance(type), 0)) body.Add(kv.Key, kv.Value);
            return body;
        }

        /// <summary>The serialized fields of <paramref name="instance"/> (no Unity header).</summary>
        public static List<KeyValuePair<string, YNode>> Fields(Type type, object instance, int depth)
        {
            var result = new List<KeyValuePair<string, YNode>>();
            if (depth == 0) result.AddRange(PackageBasePrefix(type));
            foreach (var f in SerializedFields(type))
            {
                object v = null;
                try { v = instance != null ? f.GetValue(instance) : null; } catch { }
                result.Add(new(f.Name, Value(f.FieldType, v, depth)));
            }
            return result;
        }

        /// <summary>
        /// A script deriving from a PACKAGE class inherits that class's serialized fields, which
        /// Unity writes first. The port's stand-ins for those classes do not declare Unity's
        /// private field layout, so the layout is stated here, as measured from Unity-written
        /// files. Returns nothing for MonoBehaviour / ScriptableObject.
        /// </summary>
        public static List<KeyValuePair<string, YNode>> PackageBasePrefix(Type type)
        {
            var list = new List<KeyValuePair<string, YNode>>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var t = type.BaseType; t != null; t = t.BaseType)
                if (t.Assembly == EngineAssembly) names.Add(t.Name);
            void Add(string k, YNode v) => list.Add(new(k, v));
            if (names.Contains("NetworkBehaviour")) Add("ShowTopMostFoldoutHeaderGroup", S("1"));
            if (names.Contains("Graphic") || names.Contains("MaskableGraphic"))
            {
                Add("m_Material", YMap.Ref(0));
                Add("m_Color", Flow(("r", 1), ("g", 1), ("b", 1), ("a", 1)));
                Add("m_RaycastTarget", S("1"));
                Add("m_RaycastPadding", Flow(("x", 0), ("y", 0), ("z", 0), ("w", 0)));
            }
            if (names.Contains("MaskableGraphic"))
            {
                Add("m_Maskable", S("1"));
                Add("m_OnCullStateChanged", Value(typeof(CosmicShore.Engine.Events.UnityEvent), null, 1));
            }
            if (names.Contains("RawImage") || type.Name == "RawImage")
            {
                Add("m_Texture", YMap.Ref(0));
                Add("m_UVRect", BuiltInValue(typeof(Rect), new Rect(0, 0, 1, 1)));
            }
            return list;
        }

        /// <summary>Package base classes whose serialized layout <see cref="PackageBasePrefix"/> knows.</summary>
        public static bool HasKnownBaseLayout(Type type)
        {
            for (var t = type.BaseType; t != null; t = t.BaseType)
            {
                if (t.Assembly != EngineAssembly) continue;
                if (t.Name is "MonoBehaviour" or "ScriptableObject" or "Behaviour" or "Component" or "Object"
                    or "NetworkBehaviour" or "Graphic" or "MaskableGraphic" or "RawImage" or "UIBehaviour") continue;
                return false;
            }
            return true;
        }

        // ── Which fields ──────────────────────────────────────────────

        static readonly Dictionary<Type, FieldInfo[]> s_fields = new();
        static readonly Assembly EngineAssembly = typeof(Component).Assembly;

        public static FieldInfo[] SerializedFields(Type type)
        {
            lock (s_fields)
            {
                if (s_fields.TryGetValue(type, out var cached)) return cached;
                var chain = new List<Type>();
                for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                {
                    // The engine's own classes (MonoBehaviour, Behaviour, Component, Object,
                    // ScriptableObject) carry no script-serialized fields.
                    if (t.Assembly == EngineAssembly && typeof(Engine.Object).IsAssignableFrom(t)) break;
                    chain.Add(t);
                }
                chain.Reverse();
                var list = new List<FieldInfo>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var t in chain)
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                                       .OrderBy(f => f.MetadataToken))
                    {
                        if (f.IsInitOnly || f.IsLiteral || f.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                        bool isSerializeField = HasAttr(f, "SerializeField");
                        bool isReference = HasAttr(f, "SerializeReferenceAttribute");
                        bool backing = f.Name.StartsWith('<') && f.Name.EndsWith(">k__BackingField", StringComparison.Ordinal);
                        if (f.Name.Contains('<') && !backing) continue;
                        if (!(f.IsPublic && !backing) && !isSerializeField && !isReference) continue;
                        if (isReference) continue; // [SerializeReference] is written separately (not supported yet)
                        if (!Supported(f.FieldType, 0)) continue;
                        if (seen.Add(f.Name)) list.Add(f);
                    }
                return s_fields[type] = list.ToArray();
            }
        }

        static bool HasAttr(FieldInfo f, string name)
            => f.GetCustomAttributes(false).Any(a => a.GetType().Name == name);

        /// <summary>Can Unity's serializer store a field of this type?</summary>
        public static bool Supported(Type t, int depth)
        {
            if (depth > MaxDepth) return false;
            if (IsScalar(t) || BuiltIn(t) || typeof(Engine.Object).IsAssignableFrom(t) || SerializedReader.IsUnityEvent(t)) return true;
            if (ElementType(t) is { } e)
                return ElementType(e) == null && (IsScalar(e) || BuiltIn(e) || typeof(Engine.Object).IsAssignableFrom(e)
                                                  || SerializedReader.IsUnityEvent(e) || PlainSerializable(e));
            return PlainSerializable(t);
        }

        static bool PlainSerializable(Type t)
            => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition && !t.IsPrimitive
               && !typeof(Delegate).IsAssignableFrom(t) && !typeof(Engine.Object).IsAssignableFrom(t)
               && t.IsDefined(typeof(SerializableAttribute), false)
               && !(t.IsGenericType && t.Namespace?.StartsWith("System", StringComparison.Ordinal) == true);

        static bool IsScalar(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string);

        static Type ElementType(Type t)
        {
            if (t.IsArray && t.GetArrayRank() == 1) return t.GetElementType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) return t.GetGenericArguments()[0];
            return null;
        }

        static readonly HashSet<Type> BuiltInTypes = new()
        {
            typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion), typeof(Vector2Int), typeof(Vector3Int),
            typeof(Color), typeof(Color32), typeof(Rect), typeof(Bounds), typeof(LayerMask), typeof(AnimationCurve), typeof(Gradient),
        };

        static bool BuiltIn(Type t) => BuiltInTypes.Contains(t);

        // ── Values ────────────────────────────────────────────────────

        public static YNode Value(Type t, object v, int depth)
        {
            if (t == typeof(string)) return S((string)v ?? "");
            if (t == typeof(bool)) return S(v is true ? "1" : "0");
            if (t.IsEnum) return S(Convert.ToInt64(v ?? 0, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            if (t == typeof(float)) return S(UnityYamlWriter.FormatFloat(v is float f ? f : 0f));
            if (t == typeof(double)) return S(UnityYamlWriter.FormatDouble(v is double d ? d : 0d));
            if (t == typeof(char)) return S(((int)(v is char c ? c : '\0')).ToString(CultureInfo.InvariantCulture));
            if (t.IsPrimitive) return S(Convert.ToString(v ?? Activator.CreateInstance(t), CultureInfo.InvariantCulture));

            if (BuiltIn(t)) return BuiltInValue(t, v ?? NewInstance(t));
            if (typeof(Engine.Object).IsAssignableFrom(t)) return YMap.Ref(0);
            if (SerializedReader.IsUnityEvent(t))
            {
                var calls = new YMap(); calls.Add("m_Calls", new YSeq());
                var evt = new YMap(); evt.Add("m_PersistentCalls", calls);
                return evt;
            }
            if (ElementType(t) is { } e)
            {
                var items = v is IEnumerable en ? en.Cast<object>().ToList() : new List<object>();
                if (IsIntegerLike(e)) return S(HexBlob(items)); // Unity: one little-endian hex scalar
                var seq = new YSeq();
                foreach (var item in items) seq.List.Add(Value(e, item, depth + 1));
                return seq;
            }
            // A [Serializable] class or struct: never null in Unity's output.
            var map = new YMap();
            if (depth >= MaxDepth) return map;
            v ??= NewInstance(t);
            foreach (var kv in Fields(t, v, depth + 1)) map.Add(kv.Key, kv.Value);
            return map;
        }

        static YNode BuiltInValue(Type t, object v)
        {
            switch (v)
            {
                case Vector2 a: return Flow(("x", a.x), ("y", a.y));
                case Vector3 a: return Flow(("x", a.x), ("y", a.y), ("z", a.z));
                case Vector4 a: return Flow(("x", a.x), ("y", a.y), ("z", a.z), ("w", a.w));
                case Quaternion a: return Flow(("x", a.x), ("y", a.y), ("z", a.z), ("w", a.w));
                case Vector2Int a: return FlowI(("x", a.x), ("y", a.y));
                case Vector3Int a: return FlowI(("x", a.x), ("y", a.y), ("z", a.z));
                case Color a: return Flow(("r", a.r), ("g", a.g), ("b", a.b), ("a", a.a));
                case Color32 a:
                {
                    var m = new YMap();
                    m.Add("serializedVersion", S("2"));
                    uint rgba = a.r | (uint)a.g << 8 | (uint)a.b << 16 | (uint)a.a << 24;
                    m.Add("rgba", S(rgba.ToString(CultureInfo.InvariantCulture)));
                    return m;
                }
                case Rect a:
                {
                    var m = new YMap();
                    m.Add("serializedVersion", S("2"));
                    m.Add("x", F(a.x)); m.Add("y", F(a.y)); m.Add("width", F(a.width)); m.Add("height", F(a.height));
                    return m;
                }
                case Bounds a:
                {
                    var m = new YMap();
                    m.Add("m_Center", BuiltInValue(typeof(Vector3), a.center));
                    m.Add("m_Extent", BuiltInValue(typeof(Vector3), a.extents));
                    return m;
                }
                case LayerMask a:
                {
                    var m = new YMap();
                    m.Add("serializedVersion", S("2"));
                    m.Add("m_Bits", S(unchecked((uint)a.value).ToString(CultureInfo.InvariantCulture)));
                    return m;
                }
                case AnimationCurve a: return Curve(a);
                case Gradient a: return GradientValue(a);
            }
            return new YMap();
        }

        static YMap Curve(AnimationCurve c)
        {
            var m = new YMap();
            m.Add("serializedVersion", S("2"));
            var keys = new YSeq();
            foreach (var k in c?.keys ?? Array.Empty<Keyframe>())
            {
                var km = new YMap();
                km.Add("serializedVersion", S("3"));
                km.Add("time", F(k.time)); km.Add("value", F(k.value));
                km.Add("inSlope", F(k.inTangent)); km.Add("outSlope", F(k.outTangent));
                km.Add("tangentMode", S("0")); km.Add("weightedMode", S("0"));
                km.Add("inWeight", F(0.33333334f)); km.Add("outWeight", F(0.33333334f));
                keys.List.Add(km);
            }
            m.Add("m_Curve", keys);
            m.Add("m_PreInfinity", S("2"));
            m.Add("m_PostInfinity", S("2"));
            m.Add("m_RotationOrder", S("4"));
            return m;
        }

        static YMap GradientValue(Gradient g)
        {
            var ck = g?.colorKeys ?? Array.Empty<GradientColorKey>();
            var ak = g?.alphaKeys ?? Array.Empty<GradientAlphaKey>();
            var m = new YMap();
            m.Add("serializedVersion", S("2"));
            for (int i = 0; i < 8; i++)
            {
                Color c = i < ck.Length ? ck[i].color : default;
                float a = i < ak.Length ? ak[i].alpha : 0f;
                m.Add($"key{i}", Flow(("r", c.r), ("g", c.g), ("b", c.b), ("a", a)));
            }
            for (int i = 0; i < 8; i++) m.Add($"ctime{i}", S(i < ck.Length ? Mathf.RoundToInt(ck[i].time * 65535f).ToString(CultureInfo.InvariantCulture) : "0"));
            for (int i = 0; i < 8; i++) m.Add($"atime{i}", S(i < ak.Length ? Mathf.RoundToInt(ak[i].time * 65535f).ToString(CultureInfo.InvariantCulture) : "0"));
            m.Add("m_Mode", S("0"));
            m.Add("m_ColorSpace", S("-1"));
            m.Add("m_NumColorKeys", S(ck.Length.ToString(CultureInfo.InvariantCulture)));
            m.Add("m_NumAlphaKeys", S(ak.Length.ToString(CultureInfo.InvariantCulture)));
            return m;
        }

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// A fresh instance as Unity would create it: field initializers run (that is where a
        /// script's defaults live), then null serialized containers are allocated.
        /// </summary>
        public static object NewInstance(Type t)
        {
            object o = null;
            try
            {
                if (t.IsValueType || t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) != null)
                    o = Activator.CreateInstance(t, nonPublic: true);
            }
            catch { o = null; }
            if (o == null)
            {
                try { o = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t); }
                catch { return null; }
            }
            return o;
        }

        // Unity stores an array of any integral type - enums and bools included - as one hex scalar.
        static bool IsIntegerLike(Type t)
            => t.IsEnum || t == typeof(bool) || t == typeof(int) || t == typeof(uint) || t == typeof(short) || t == typeof(ushort)
            || t == typeof(long) || t == typeof(ulong) || t == typeof(byte) || t == typeof(sbyte) || t == typeof(char);

        static string HexBlob(List<object> items)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var item in items)
            {
                byte[] bytes = item switch
                {
                    int v => BitConverter.GetBytes(v), uint v => BitConverter.GetBytes(v),
                    short v => BitConverter.GetBytes(v), ushort v => BitConverter.GetBytes(v),
                    long v => BitConverter.GetBytes(v), ulong v => BitConverter.GetBytes(v),
                    char v => BitConverter.GetBytes((ushort)v), byte v => new[] { v }, sbyte v => new[] { unchecked((byte)v) },
                    Enum v => EnumBytes(v),
                    bool v => new[] { v ? (byte)1 : (byte)0 },
                    _ => Array.Empty<byte>(),
                };
                foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        static byte[] EnumBytes(Enum v)
        {
            var u = Enum.GetUnderlyingType(v.GetType());
            long n = Convert.ToInt64(v, CultureInfo.InvariantCulture);
            return u == typeof(byte) || u == typeof(sbyte) ? new[] { unchecked((byte)n) }
                 : u == typeof(short) || u == typeof(ushort) ? BitConverter.GetBytes(unchecked((short)n))
                 : u == typeof(long) || u == typeof(ulong) ? BitConverter.GetBytes(n)
                 : BitConverter.GetBytes(unchecked((int)n));
        }

        static YScalar S(string s) => new(s);
        static YScalar F(float f) => new(UnityYamlWriter.FormatFloat(f));

        static YMap Flow(params (string k, float v)[] kv)
        {
            var m = new YMap { Flow = true };
            foreach (var (k, v) in kv) m.Add(k, F(v));
            return m;
        }

        static YMap FlowI(params (string k, int v)[] kv)
        {
            var m = new YMap { Flow = true };
            foreach (var (k, v) in kv) m.Add(k, S(v.ToString(CultureInfo.InvariantCulture)));
            return m;
        }
    }
}
