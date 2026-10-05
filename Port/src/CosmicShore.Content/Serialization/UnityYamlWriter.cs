using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Engine;

namespace CosmicShore.Content.Serialization
{
    /// <summary>
    /// Writes a ScriptableObject back as the Unity YAML its editor would write, so data the
    /// port produced (a trained population, an archive) goes back into the project as a
    /// drop-in asset file rather than as JSON someone has to paste through a tool.
    ///
    /// The document header (the <c>%YAML</c>/<c>%TAG</c> lines, the <c>--- !u!114</c> line and
    /// the engine's <c>m_*</c> fields up to <c>m_EditorClassIdentifier</c>) is copied from the
    /// asset being replaced, so the script guid and name stay exactly as the editor wrote them.
    /// The body follows the editor's serializer: base-class fields first, then declaration
    /// order; public fields and <c>[SerializeField]</c> private ones, minus readonly, const and
    /// <c>[NonSerialized]</c>; a null serializable class is written as its default instance
    /// (the editor never serializes a null there); integer arrays and lists are ONE hex blob of
    /// little-endian bytes (what <see cref="SerializedReader.TryDecodeHexBlob"/> reads), float
    /// and every other element type a block sequence; an empty list is <c>[]</c>; a bool is 0/1
    /// and an enum its number; object references are written as a null reference (the port has
    /// no way to name an asset it did not load — the types this writer is used for have none).
    /// </summary>
    public static class UnityYamlWriter
    {
        /// <summary>Resolves an object reference to "{fileID: F, guid: G, type: T}" (null = write a null reference).</summary>
        [ThreadStatic] static Func<CosmicShore.Engine.Object, string> s_refs;

        public static string WriteScriptableObject(object asset, string templateAssetText, Func<CosmicShore.Engine.Object, string> references = null)
        {
            s_refs = references;
            var sb = new StringBuilder();
            foreach (var line in templateAssetText.Replace("\r\n", "\n").Split('\n'))
            {
                sb.Append(line).Append('\n');
                if (line.StartsWith("  m_EditorClassIdentifier:", StringComparison.Ordinal)) break;
            }
            WriteFields(sb, asset, 2);
            return sb.ToString();
        }

        static void WriteFields(StringBuilder sb, object obj, int indent)
        {
            foreach (var f in SerializedFields(obj.GetType()))
            {
                // A ScriptableObject's own engine fields came from the template header.
                if (f.Name.StartsWith("m_", StringComparison.Ordinal) && f.DeclaringType.Assembly == typeof(ScriptableObject).Assembly) continue;
                WriteMember(sb, f.Name, f.FieldType, f.GetValue(obj), indent);
            }
        }

        static void WriteMember(StringBuilder sb, string name, Type type, object value, int indent)
        {
            string pad = new string(' ', indent);
            if (IsScalar(type))
            {
                sb.Append(pad).Append(name).Append(':');
                string s = Scalar(type, value);
                if (s.Length > 0) sb.Append(' ').Append(s);
                else sb.Append(' ');
                sb.Append('\n');
                return;
            }
            if (typeof(CosmicShore.Engine.Object).IsAssignableFrom(type))
            {
                sb.Append(pad).Append(name).Append(": ").Append(Reference(value)).Append('\n');
                return;
            }
            if (ElementType(type) is { } elem)
            {
                var items = value is IEnumerable e ? e.Cast<object>().ToList() : new List<object>();
                if (items.Count == 0) { sb.Append(pad).Append(name).Append(": []\n"); return; }
                if (IsIntegerLike(elem))
                {
                    sb.Append(pad).Append(name).Append(": ").Append(HexBlob(elem, items)).Append('\n');
                    return;
                }
                sb.Append(pad).Append(name).Append(":\n");
                foreach (var item in items) WriteSequenceItem(sb, elem, item, indent);
                return;
            }
            // A nested [Serializable] class/struct: never null in the editor's output.
            value ??= NewDefault(type);
            sb.Append(pad).Append(name).Append(":\n");
            WriteFields(sb, value, indent + 2);
        }

        static void WriteSequenceItem(StringBuilder sb, Type elem, object item, int indent)
        {
            string pad = new string(' ', indent);
            if (IsScalar(elem))
            {
                string s = Scalar(elem, item);
                sb.Append(pad).Append("- ").Append(s).Append('\n');
                return;
            }
            if (typeof(CosmicShore.Engine.Object).IsAssignableFrom(elem))
            {
                sb.Append(pad).Append("- ").Append(Reference(item)).Append('\n');
                return;
            }
            // "- first: v" then the remaining fields at indent + 2.
            item ??= NewDefault(elem);
            var inner = new StringBuilder();
            WriteFields(inner, item, indent + 2);
            var text = inner.ToString();
            if (text.Length == 0) { sb.Append(pad).Append("- {}\n"); return; }
            sb.Append(pad).Append("- ").Append(text, indent + 2, text.Length - (indent + 2));
        }

        static string Reference(object value)
            => value is CosmicShore.Engine.Object o && o != null && s_refs?.Invoke(o) is { } r ? r : "{fileID: 0}";

        // ── Fields ─────────────────────────────────────────────────────

        static readonly Dictionary<Type, FieldInfo[]> s_fields = new();

        public static FieldInfo[] SerializedFields(Type type)
        {
            lock (s_fields)
            {
                if (s_fields.TryGetValue(type, out var cached)) return cached;
                var chain = new List<Type>();
                for (var t = type; t != null && t != typeof(object) && t != typeof(ScriptableObject)
                     && t != typeof(CosmicShore.Engine.Object) && t != typeof(MonoBehaviour); t = t.BaseType)
                    chain.Add(t);
                chain.Reverse(); // base class first, as the editor writes
                var list = new List<FieldInfo>();
                var seen = new HashSet<string>();
                foreach (var t in chain)
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                                       .OrderBy(f => f.MetadataToken))
                    {
                        if (f.IsInitOnly || f.IsLiteral || f.IsNotSerialized) continue;
                        if (f.Name.Contains('<')) continue;
                        if (!f.IsPublic && f.GetCustomAttribute<SerializeFieldAttribute>() == null) continue;
                        if (!Serializable(f.FieldType)) continue;
                        if (seen.Add(f.Name)) list.Add(f);
                    }
                return s_fields[type] = list.ToArray();
            }
        }

        static bool Serializable(Type t)
        {
            if (IsScalar(t) || typeof(CosmicShore.Engine.Object).IsAssignableFrom(t)) return true;
            if (ElementType(t) is { } e) return IsScalar(e) || typeof(CosmicShore.Engine.Object).IsAssignableFrom(e) || Compound(e);
            return Compound(t);
        }

        static bool Compound(Type t)
            => !t.IsAbstract && !t.IsInterface && !typeof(Delegate).IsAssignableFrom(t)
               && t.GetCustomAttribute<SerializableAttribute>() != null;

        static Type ElementType(Type t)
        {
            if (t.IsArray && t.GetArrayRank() == 1) return t.GetElementType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) return t.GetGenericArguments()[0];
            return null;
        }

        /// <summary>The editor's default instance: field initializers run when there is a parameterless constructor.</summary>
        static object NewDefault(Type t)
            => t.IsValueType || t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) != null
                ? Activator.CreateInstance(t, nonPublic: true)
                : System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t);

        static bool IsScalar(Type t)
            => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);

        static bool IsIntegerLike(Type t)
            => t == typeof(int) || t == typeof(uint) || t == typeof(short) || t == typeof(ushort)
            || t == typeof(long) || t == typeof(ulong) || t == typeof(byte) || t == typeof(sbyte) || t == typeof(char);

        // ── Scalars ────────────────────────────────────────────────────

        static string Scalar(Type type, object value)
        {
            if (value == null) return "";
            if (type == typeof(bool)) return (bool)value ? "1" : "0";
            if (type.IsEnum) return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            if (type == typeof(float)) return Float((float)value);
            if (type == typeof(double)) return Double((double)value);
            if (type == typeof(string)) return Str((string)value);
            if (type == typeof(char)) return ((int)(char)value).ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        internal static string FormatFloat(float f) => Float(f);
        internal static string FormatDouble(double d) => Double(d);

        static string Float(float f)
        {
            if (float.IsPositiveInfinity(f)) return "Infinity";
            if (float.IsNegativeInfinity(f)) return "-Infinity";
            if (float.IsNaN(f)) return "NaN";
            return Exponent(f.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>The editor writes exponents as <c>1e-7</c>, not .NET's <c>1E-07</c>.</summary>
        static string Exponent(string s)
        {
            int e = s.IndexOf('E');
            if (e < 0) return s;
            string mant = s[..e], exp = s[(e + 1)..];
            char sign = exp[0] == '-' ? '-' : '+';
            string digits = exp.TrimStart('+', '-').TrimStart('0');
            return mant + "e" + sign + (digits.Length == 0 ? "0" : digits);
        }

        static string Double(double d)
        {
            if (double.IsPositiveInfinity(d)) return "Infinity";
            if (double.IsNegativeInfinity(d)) return "-Infinity";
            if (double.IsNaN(d)) return "NaN";
            return Exponent(d.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Plain when YAML allows it — the editor writes 2026-09-29T15:08:32Z bare, since a colon
        /// only needs quoting before a space — else double-quoted, with the editor's escapes:
        /// \xHH for Latin-1, \uXXXX beyond it.
        /// </summary>
        static string Str(string s)
        {
            if (s.Length == 0) return "";
            bool plain = !s.Contains(": ") && !s.Contains(" #") && !s.EndsWith(':')
                && s.IndexOfAny(new[] { '\n', '\r', '\t', '"' }) < 0
                && "[]{}!&*|>%@`,?'#".IndexOf(s[0]) < 0
                && !(s[0] == '-' && (s.Length == 1 || s[1] == ' '))
                && s[0] != ' ' && s[^1] != ' '
                && s.All(c => c >= 0x20 && c <= 0x7E);
            if (plain) return s;
            var sb = new StringBuilder("\"");
            foreach (char c in s)
                sb.Append(c switch
                {
                    '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                    _ when c < 0x20 => $"\\x{(int)c:X2}",
                    _ when c > 0x7E && c <= 0xFF => $"\\x{(int)c:X2}",
                    _ when c > 0xFF => $"\\u{(int)c:X4}",
                    _ => c.ToString(),
                });
            return sb.Append('"').ToString();
        }

        static string HexBlob(Type elem, List<object> items)
        {
            var sb = new StringBuilder(items.Count * 8);
            foreach (var item in items)
            {
                byte[] bytes = item switch
                {
                    int v => BitConverter.GetBytes(v),
                    uint v => BitConverter.GetBytes(v),
                    short v => BitConverter.GetBytes(v),
                    ushort v => BitConverter.GetBytes(v),
                    long v => BitConverter.GetBytes(v),
                    ulong v => BitConverter.GetBytes(v),
                    char v => BitConverter.GetBytes((ushort)v),
                    byte v => new[] { v },
                    sbyte v => new[] { unchecked((byte)v) },
                    _ => Array.Empty<byte>(),
                };
                foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
