using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Content.Serialization
{
    /// <summary>Resolves serialized object references for <see cref="SerializedReader"/>.</summary>
    public interface IReferenceResolver
    {
        /// <summary>
        /// Returns the object a reference names, adapted to <paramref name="fieldType"/>
        /// (e.g. a GameObject reference read into a Transform field yields its transform),
        /// or null when it cannot be resolved.
        /// </summary>
        object Resolve(ObjRef reference, Type fieldType, AssetFile origin);

        /// <summary>Called for UnityEvent fields so persistent calls can be wired once every object exists.</summary>
        void OnUnityEvent(object unityEvent, YNode persistentCalls, AssetFile origin);
    }

    /// <summary>
    /// Maps a serialized YAML body onto a .NET object by reflection, following the Unity
    /// serializer's naming: a key matches a field of the same name, or — because the
    /// engine's hand-written built-ins sometimes expose the public spelling — the
    /// <c>m_Foo</c> key matches <c>foo</c>/<c>Foo</c>, and <c>[FormerlySerializedAs]</c>
    /// names match too. Unknown keys are ignored (exactly like Unity loading data written
    /// by an older or newer script version).
    /// </summary>
    public sealed class SerializedReader
    {
        static readonly HashSet<string> SkipKeys = new(StringComparer.Ordinal)
        {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
            "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier",
            "serializedVersion",
        };

        readonly IReferenceResolver _resolver;

        public SerializedReader(IReferenceResolver resolver) { _resolver = resolver; }

        /// <summary>Fills <paramref name="target"/>'s members from <paramref name="body"/>.</summary>
        public void ReadInto(object target, YMap body, AssetFile origin)
        {
            if (target == null || body == null) return;
            var members = MembersOf(target.GetType());
            foreach (var entry in body.Entries)
            {
                if (SkipKeys.Contains(entry.Key)) continue;
                if (!members.TryGetValue(entry.Key, out var member)) continue;
                try
                {
                    object existing = member.Get(target);
                    object value = ReadValue(entry.Value, member.Type, existing, origin);
                    if (value != null || !member.Type.IsValueType) member.Set(target, value);
                }
                catch (Exception)
                {
                    // One bad field must never sink the object (Unity: logs and moves on).
                }
            }
        }

        public object ReadValue(YNode node, Type type, object existing, AssetFile origin)
        {
            if (node == null) return existing;

            if (type == typeof(string)) return node.Scalar ?? "";
            if (type == typeof(bool)) return node.Scalar == "1" || string.Equals(node.Scalar, "true", StringComparison.OrdinalIgnoreCase);
            if (type == typeof(float)) return YScalar.TryFloat(node.Scalar, out var f) ? f : 0f;
            if (type == typeof(double)) return double.TryParse(node.Scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0d;
            if (type.IsEnum)
            {
                YScalar.TryLong(node.Scalar, out var ev);
                return Enum.ToObject(type, ev);
            }
            if (type.IsPrimitive)
            {
                YScalar.TryLong(node.Scalar, out var lv);
                try { return Convert.ChangeType(lv, type, CultureInfo.InvariantCulture); }
                catch { return unchecked(Convert.ChangeType((ulong)lv & MaskFor(type), type, CultureInfo.InvariantCulture)); }
            }

            // Engine value types.
            if (type == typeof(Vector2)) return new Vector2(node.Float("x"), node.Float("y"));
            if (type == typeof(Vector3)) return new Vector3(node.Float("x"), node.Float("y"), node.Float("z"));
            if (type == typeof(Vector4)) return new Vector4(node.Float("x"), node.Float("y"), node.Float("z"), node.Float("w"));
            if (type == typeof(Quaternion)) return new Quaternion(node.Float("x"), node.Float("y"), node.Float("z"), node.Float("w", 1f));
            if (type == typeof(Vector2Int)) return new Vector2Int(node.Int("x"), node.Int("y"));
            if (type == typeof(Vector3Int)) return new Vector3Int(node.Int("x"), node.Int("y"), node.Int("z"));
            if (type == typeof(Color)) return ReadColor(node);
            if (type == typeof(Color32)) return ReadColor32(node);
            if (type == typeof(Rect)) return new Rect(node.Float("x"), node.Float("y"), node.Float("width"), node.Float("height"));
            if (type == typeof(Bounds))
            {
                var c = node["m_Center"]; var e = node["m_Extent"];
                return new Bounds(new Vector3(c?.Float("x") ?? 0, c?.Float("y") ?? 0, c?.Float("z") ?? 0),
                    2f * new Vector3(e?.Float("x") ?? 0, e?.Float("y") ?? 0, e?.Float("z") ?? 0));
            }
            if (type == typeof(LayerMask)) return new LayerMask { value = (int)node.Long("m_Bits") };
            if (type == typeof(AnimationCurve)) return ReadCurve(node);
            if (type == typeof(Gradient)) return ReadGradient(node);

            // Object references.
            if (typeof(CosmicShore.Engine.Object).IsAssignableFrom(type))
            {
                if (node is not YMap) return null;
                var r = ObjRef.From(node);
                if (r.IsNull) return null;
                return _resolver?.Resolve(r, type, origin);
            }

            // UnityEvent family: persistent calls are wired later by the resolver.
            if (IsUnityEvent(type))
            {
                object evt = existing ?? CreateInstance(type);
                if (evt != null) _resolver?.OnUnityEvent(evt, node["m_PersistentCalls"], origin);
                return evt;
            }

            // Arrays / lists.
            if (type.IsArray)
            {
                var elem = type.GetElementType();
                var items = node.Items;
                var arr = Array.CreateInstance(elem, items.Count);
                for (int i = 0; i < items.Count; i++) arr.SetValue(ReadValue(items[i], elem, null, origin), i);
                return arr;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elem = type.GetGenericArguments()[0];
                var list = (IList)(existing ?? Activator.CreateInstance(type));
                list.Clear();
                foreach (var item in node.Items) list.Add(ReadValue(item, elem, null, origin));
                return list;
            }

            // Nested [Serializable] class / struct.
            if (node is YMap map && !type.IsInterface && !type.IsAbstract)
            {
                object obj = existing ?? CreateInstance(type);
                if (obj == null) return existing;
                ReadInto(obj, map, origin);
                return obj;
            }
            return existing;
        }

        static ulong MaskFor(Type t) => t == typeof(byte) || t == typeof(sbyte) ? 0xFF
            : t == typeof(short) || t == typeof(ushort) || t == typeof(char) ? 0xFFFF
            : t == typeof(int) || t == typeof(uint) ? 0xFFFF_FFFF : ulong.MaxValue;

        public static Color ReadColor(YNode n)
            => new(n.Float("r"), n.Float("g"), n.Float("b"), n.Float("a", 1f));

        public static Color32 ReadColor32(YNode n)
        {
            if (n["rgba"] != null)
            {
                uint v = unchecked((uint)n.Long("rgba"));
                return new Color32((byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)(v >> 24));
            }
            return new Color32((byte)n.Int("r"), (byte)n.Int("g"), (byte)n.Int("b"), (byte)n.Int("a", 255));
        }

        static AnimationCurve ReadCurve(YNode n)
        {
            var curve = new AnimationCurve();
            foreach (var k in n["m_Curve"]?.Items ?? Array.Empty<YNode>())
                curve.AddKey(new Keyframe(k.Float("time"), k.Float("value"), k.Float("inSlope"), k.Float("outSlope")));
            return curve;
        }

        static Gradient ReadGradient(YNode n)
        {
            var g = new Gradient();
            int nc = n.Int("m_NumColorKeys", 2), na = n.Int("m_NumAlphaKeys", 2);
            var ck = new GradientColorKey[Math.Clamp(nc, 0, 8)];
            var ak = new GradientAlphaKey[Math.Clamp(na, 0, 8)];
            for (int i = 0; i < ck.Length; i++)
            {
                var c = n[$"key{i}"];
                ck[i] = new GradientColorKey(c != null ? new Color(c.Float("r"), c.Float("g"), c.Float("b"), 1f) : Color.white, n.Int($"ctime{i}") / 65535f);
            }
            for (int i = 0; i < ak.Length; i++)
            {
                var c = n[$"key{i}"];
                ak[i] = new GradientAlphaKey(c?.Float("a", 1f) ?? 1f, n.Int($"atime{i}") / 65535f);
            }
            g.SetKeys(ck, ak);
            return g;
        }

        internal static bool IsUnityEvent(Type t)
        {
            for (var b = t; b != null && b != typeof(object); b = b.BaseType)
            {
                if (b == typeof(CosmicShore.Engine.Events.UnityEvent)) return true;
                if (b.IsGenericType && b.Namespace == "CosmicShore.Engine.Events" && b.Name.StartsWith("UnityEvent`", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        static object CreateInstance(Type type)
        {
            try { return Activator.CreateInstance(type, nonPublic: true); }
            catch
            {
                try { return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type); }
                catch { return null; }
            }
        }

        // ── Member tables ────────────────────────────────────────────────────

        sealed class Member
        {
            public Type Type;
            public Func<object, object> Get;
            public Action<object, object> Set;
        }

        static readonly ConcurrentDictionary<Type, Dictionary<string, Member>> s_members = new();

        static Dictionary<string, Member> MembersOf(Type type) => s_members.GetOrAdd(type, Build);

        static Dictionary<string, Member> Build(Type type)
        {
            var result = new Dictionary<string, Member>(StringComparer.Ordinal);
            var aliases = new Dictionary<string, Member>(StringComparer.Ordinal);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var f in t.GetFields(flags))
                {
                    if (f.IsInitOnly || f.IsLiteral || f.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                    if (f.Name.Contains('<')) continue; // compiler-generated backing fields
                    var m = new Member { Type = f.FieldType, Get = f.GetValue, Set = f.SetValue };
                    result.TryAdd(f.Name, m);
                    foreach (var fs in f.GetCustomAttributes<FormerlySerializedAsAttribute>(false))
                        aliases.TryAdd(fs.oldName, m);
                    AddSpellings(aliases, f.Name, m);
                }
                foreach (var p in t.GetProperties(flags))
                {
                    if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0) continue;
                    var m = new Member { Type = p.PropertyType, Get = o => p.GetValue(o), Set = (o, v) => p.SetValue(o, v) };
                    aliases.TryAdd(p.Name, m);
                    AddSpellings(aliases, p.Name, m);
                }
            }
            foreach (var kv in aliases) result.TryAdd(kv.Key, kv.Value);
            return result;
        }

        // A member "foo" / "Foo" also answers the serialized key "m_Foo".
        static void AddSpellings(Dictionary<string, Member> aliases, string name, Member m)
        {
            if (name.StartsWith("m_", StringComparison.Ordinal) || name.Length == 0) return;
            string upper = char.ToUpperInvariant(name[0]) + name.Substring(1);
            aliases.TryAdd("m_" + upper, m);
            aliases.TryAdd("m_" + name, m);
        }
    }
}
