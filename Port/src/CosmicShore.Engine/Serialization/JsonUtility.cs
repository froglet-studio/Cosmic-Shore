using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace CosmicShore.Engine
{
    /// <summary>
    /// UnityEngine.JsonUtility — the engine's field-based JSON serializer, implemented from
    /// its documented rules:
    /// <list type="bullet">
    /// <item>Serializes the same fields the inspector does: public instance fields and
    /// <c>[SerializeField]</c> private ones, minus <c>[NonSerialized]</c>, static, const and
    /// readonly. Properties are never serialized.</item>
    /// <item>Supported values: primitives, string, enums (as their integer), arrays and
    /// <c>List&lt;T&gt;</c> of supported values, <c>[Serializable]</c> classes and structs, and the
    /// engine value types (vectors, quaternion, color, rect, bounds…) as their fields.
    /// Dictionaries, multidimensional arrays and nested collections are skipped, as in the
    /// original.</item>
    /// <item>A <see cref="Object"/> reference writes as <c>{"instanceID":n}</c> and reads back
    /// to the same live object within the session.</item>
    /// <item><see cref="FromJsonOverwrite"/> only writes fields present in the JSON, which is
    /// what lets it copy one config into another in place.</item>
    /// </list>
    /// </summary>
    public static class JsonUtility
    {
        static readonly Dictionary<int, WeakReference<Object>> s_objects = new();
        static readonly Dictionary<Type, FieldInfo[]> s_fields = new();
        static readonly object s_gate = new();

        public static string ToJson(object obj) => ToJson(obj, false);

        public static string ToJson(object obj, bool prettyPrint)
        {
            if (obj == null) return string.Empty;
            var type = obj.GetType();
            if (IsLeaf(type) || type.IsArray || IsList(type))
                throw new ArgumentException("JsonUtility.ToJson does not support engine types.", nameof(obj));

            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = prettyPrint, IndentSize = 4 }))
                WriteObject(w, obj, type);
            return Encoding.UTF8.GetString(stream.ToArray()); // the original indents with four spaces
        }

        public static T FromJson<T>(string json) => (T)FromJson(json, typeof(T));

        public static object FromJson(string json, Type type)
        {
            if (string.IsNullOrEmpty(json)) return type.IsValueType ? Activator.CreateInstance(type) : null;
            if (typeof(Object).IsAssignableFrom(type))
                throw new ArgumentException("Cannot deserialize JSON to new instances of type '" + type.Name + ".'");
            object target = type.IsValueType ? Activator.CreateInstance(type) : CreateInstance(type);
            using var doc = JsonDocument.Parse(json);
            ReadInto(doc.RootElement, ref target, type);
            return target;
        }

        public static void FromJsonOverwrite(string json, object objectToOverwrite)
        {
            if (string.IsNullOrEmpty(json) || objectToOverwrite == null) return;
            using var doc = JsonDocument.Parse(json);
            object target = objectToOverwrite;
            ReadInto(doc.RootElement, ref target, target.GetType());
        }

        // ── field model ──────────────────────────────────────────────────────

        internal static FieldInfo[] SerializedFields(Type type)
        {
            lock (s_gate)
            {
                if (s_fields.TryGetValue(type, out var cached)) return cached;
                var list = new List<FieldInfo>();
                var seen = new HashSet<string>();
                for (var t = type; t != null && t != typeof(object) && t != typeof(Object)
                     && t != typeof(ScriptableObject) && t != typeof(MonoBehaviour) && t != typeof(Behaviour)
                     && t != typeof(Component); t = t.BaseType)
                {
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsInitOnly || f.IsLiteral || f.IsNotSerialized) continue;
                        if (f.Name.Contains('<')) continue; // compiler-generated backing fields
                        if (!f.IsPublic && f.GetCustomAttribute<SerializeFieldAttribute>() == null) continue;
                        if (!IsSerializableType(f.FieldType)) continue;
                        if (!seen.Add(f.Name)) continue;
                        list.Add(f);
                    }
                }
                var result = list.ToArray();
                s_fields[type] = result;
                return result;
            }
        }

        static bool IsSerializableType(Type t)
        {
            if (IsLeaf(t) || typeof(Object).IsAssignableFrom(t)) return true;
            if (t.IsArray) return t.GetArrayRank() == 1 && IsElementType(t.GetElementType());
            if (IsList(t)) return IsElementType(t.GetGenericArguments()[0]);
            if (t.IsGenericType && typeof(IDictionary).IsAssignableFrom(t)) return false;
            if (typeof(Delegate).IsAssignableFrom(t) || t.IsPointer || t.IsInterface || t.IsAbstract) return false;
            return IsSerializableCompound(t);
        }

        static bool IsElementType(Type t) => !t.IsArray && !IsList(t) && IsSerializableType(t);

        static bool IsSerializableCompound(Type t)
            => t.IsDefined(typeof(SerializableAttribute), false) || t.Namespace == typeof(Vector3).Namespace && t.IsValueType;

        static bool IsLeaf(Type t)
            => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);

        static bool IsList(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);

        static object CreateInstance(Type t)
        {
            try { return Activator.CreateInstance(t, nonPublic: true); }
            catch (MissingMethodException) { return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t); }
        }

        // ── write ────────────────────────────────────────────────────────────

        static void WriteObject(Utf8JsonWriter w, object obj, Type type)
        {
            w.WriteStartObject();
            foreach (var f in SerializedFields(type))
            {
                w.WritePropertyName(f.Name);
                WriteValue(w, f.GetValue(obj), f.FieldType, depth: 0);
            }
            w.WriteEndObject();
        }

        static void WriteValue(Utf8JsonWriter w, object value, Type type, int depth)
        {
            if (typeof(Object).IsAssignableFrom(type))
            {
                w.WriteStartObject();
                var o = value as Object;
                int id = o ? o.GetInstanceID() : 0;
                if (id != 0) lock (s_gate) s_objects[id] = new WeakReference<Object>(o);
                w.WriteNumber("instanceID", id);
                w.WriteEndObject();
                return;
            }
            if (type.IsEnum) { w.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture)); return; }
            switch (value)
            {
                case string s: w.WriteStringValue(s); return;
                case bool b: w.WriteBooleanValue(b); return;
                case float fl: WriteFloat(w, fl); return;
                case double d: w.WriteNumberValue(d); return;
                case char c: w.WriteNumberValue(c); return;
            }
            if (type.IsPrimitive || type == typeof(decimal))
            {
                w.WriteNumberValue(Convert.ToDecimal(value, CultureInfo.InvariantCulture));
                return;
            }
            if (type == typeof(string)) { w.WriteStringValue(string.Empty); return; }
            if (type.IsArray || IsList(type))
            {
                var elem = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                w.WriteStartArray();
                if (value is IList list)
                    foreach (var item in list) WriteValue(w, item, elem, depth + 1);
                w.WriteEndArray();
                return;
            }
            // Serializable compound: null class fields serialize as a default instance, like
            // the original (which never writes null for a serializable class).
            if (depth > 10) { w.WriteStartObject(); w.WriteEndObject(); return; }
            value ??= CreateInstance(type);
            w.WriteStartObject();
            foreach (var f in SerializedFields(type))
            {
                w.WritePropertyName(f.Name);
                WriteValue(w, f.GetValue(value), f.FieldType, depth + 1);
            }
            w.WriteEndObject();
        }

        static void WriteFloat(Utf8JsonWriter w, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) { w.WriteNumberValue(0); return; }
            // Round-trip precision without the float→double widening noise.
            w.WriteRawValue(value.ToString("R", CultureInfo.InvariantCulture));
        }

        // ── read ─────────────────────────────────────────────────────────────

        static void ReadInto(JsonElement json, ref object target, Type type)
        {
            if (json.ValueKind != JsonValueKind.Object) return;
            foreach (var f in SerializedFields(type))
            {
                if (!json.TryGetProperty(f.Name, out var prop)) continue;
                var current = f.GetValue(target);
                if (TryRead(prop, f.FieldType, current, out var value)) f.SetValue(target, value);
            }
        }

        static bool TryRead(JsonElement json, Type type, object current, out object value)
        {
            value = null;
            try
            {
                if (typeof(Object).IsAssignableFrom(type))
                {
                    if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("instanceID", out var idEl)) return false;
                    int id = idEl.GetInt32();
                    if (id == 0) return true;
                    lock (s_gate)
                        if (s_objects.TryGetValue(id, out var weak) && weak.TryGetTarget(out var o) && type.IsInstanceOfType(o))
                        { value = o; return true; }
                    return false;
                }
                if (type == typeof(string)) { value = json.ValueKind == JsonValueKind.String ? json.GetString() : json.ToString(); return true; }
                if (type == typeof(bool)) { value = json.ValueKind == JsonValueKind.True || json.ValueKind == JsonValueKind.Number && json.GetDouble() != 0; return true; }
                if (type.IsEnum)
                {
                    value = json.ValueKind == JsonValueKind.String
                        ? Enum.Parse(type, json.GetString(), ignoreCase: true)
                        : Enum.ToObject(type, json.GetInt64());
                    return true;
                }
                if (type.IsPrimitive || type == typeof(decimal))
                {
                    if (json.ValueKind != JsonValueKind.Number) return false;
                    if (type == typeof(char)) { value = (char)json.GetInt32(); return true; }
                    value = Convert.ChangeType(json.GetDecimal(), type, CultureInfo.InvariantCulture);
                    return true;
                }
                if (type.IsArray || IsList(type))
                {
                    if (json.ValueKind != JsonValueKind.Array) return false;
                    var elem = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                    var items = new List<object>();
                    var existing = current as IList;
                    int i = 0;
                    foreach (var item in json.EnumerateArray())
                    {
                        var prior = existing != null && i < existing.Count ? existing[i] : null;
                        items.Add(TryRead(item, elem, prior, out var v) ? v : DefaultOf(elem));
                        i++;
                    }
                    if (type.IsArray)
                    {
                        var arr = Array.CreateInstance(elem, items.Count);
                        for (int k = 0; k < items.Count; k++) arr.SetValue(items[k], k);
                        value = arr;
                    }
                    else
                    {
                        var list = (IList)Activator.CreateInstance(type);
                        foreach (var v in items) list.Add(v);
                        value = list;
                    }
                    return true;
                }
                if (json.ValueKind != JsonValueKind.Object) return false;
                object target = current ?? (type.IsValueType ? Activator.CreateInstance(type) : CreateInstance(type));
                ReadInto(json, ref target, type);
                value = target;
                return true;
            }
            catch (Exception e) when (e is FormatException or OverflowException or InvalidOperationException or ArgumentException)
            {
                return false;
            }
        }

        static object DefaultOf(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
    }
}
