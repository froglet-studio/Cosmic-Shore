using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace CosmicShore.Engine
{
    /// <summary>
    /// The serializer's non-null guarantee (original contract): a freshly created script
    /// instance — AddComponent, ScriptableObject.CreateInstance, or a component whose data
    /// omits a field — never holds null in a serialized string, array, <see cref="List{T}"/>
    /// or [Serializable] plain class. The engine's serializer allocates them (empty string,
    /// empty container, default-constructed object, recursively to its depth limit) before
    /// Awake, so game code routinely iterates a serialized list it never initialized.
    /// [SerializeReference] fields are exempt: they are genuinely nullable.
    /// </summary>
    public static class SerializedFieldDefaults
    {
        const int MaxDepth = 10; // the original serializer's nesting limit
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly ConcurrentDictionary<Type, FieldInfo[]> s_Fields = new();
        static readonly Assembly s_EngineAssembly = typeof(Component).Assembly;

        /// <summary>Fill every null serialized field of a script instance (engine-defined types are skipped — they own their defaults).</summary>
        public static void Fill(object instance)
        {
            if (instance == null || instance.GetType().Assembly == s_EngineAssembly) return;
            FillObject(instance, 0);
        }

        static void FillObject(object instance, int depth)
        {
            if (depth > MaxDepth) return;
            foreach (var field in FieldsOf(instance.GetType()))
            {
                if (field.GetValue(instance) != null) continue;
                var value = CreateDefault(field.FieldType, depth);
                if (value != null) field.SetValue(instance, value);
            }
        }

        static object CreateDefault(Type type, int depth)
        {
            if (type == typeof(string)) return string.Empty;
            if (type.IsArray)
                return type.GetArrayRank() == 1 ? Array.CreateInstance(type.GetElementType()!, 0) : null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return Activator.CreateInstance(type);
            if (!IsSerializablePlainClass(type)) return null;
            var ctor = type.GetConstructor(Instance, null, Type.EmptyTypes, null);
            if (ctor == null) return null;
            object obj;
            try { obj = ctor.Invoke(null); }
            catch { return null; }
            FillObject(obj, depth + 1);
            return obj;
        }

        static bool IsSerializablePlainClass(Type type)
            => type.IsClass && !type.IsAbstract && !type.IsGenericTypeDefinition
               && !typeof(Object).IsAssignableFrom(type)
               && !typeof(Delegate).IsAssignableFrom(type)
               && type.IsDefined(typeof(SerializableAttribute), inherit: false)
               && !type.IsGenericType; // the original serializer skips open-ish generic plain classes except List<>

        static FieldInfo[] FieldsOf(Type type) => s_Fields.GetOrAdd(type, static t =>
        {
            var list = new List<FieldInfo>();
            for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
            {
                if (cur.Assembly == s_EngineAssembly) break;
                foreach (var f in cur.GetFields(Instance | BindingFlags.DeclaredOnly))
                {
                    if (f.IsStatic || f.IsInitOnly || f.IsLiteral) continue;
                    if (f.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                    if (f.GetCustomAttributes(false) is var attrs && Array.Exists(attrs, a => a.GetType().Name == "SerializeReferenceAttribute")) continue;
                    bool serialized = f.IsPublic || Array.Exists(f.GetCustomAttributes(false), a => a.GetType().Name == "SerializeFieldAttribute");
                    if (!serialized) continue;
                    var ft = f.FieldType;
                    if (ft == typeof(string) || ft.IsArray
                        || (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
                        || IsSerializablePlainClass(ft))
                        list.Add(f);
                }
            }
            return list.ToArray();
        });
    }
}
