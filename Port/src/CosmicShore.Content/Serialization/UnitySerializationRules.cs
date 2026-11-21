#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Engine;

namespace CosmicShore.Content.Serialization
{
    /// <summary>
    /// Which fields Unity's script serializer writes and reads, for script types (the game's own
    /// MonoBehaviours, ScriptableObjects and [Serializable] classes): instance fields that are
    /// public or carry [SerializeField], are not [NonSerialized], readonly or const - including an
    /// auto-property's backing field when the property says [field: SerializeField] (YAML key
    /// "&lt;Name&gt;k__BackingField") - and whose type Unity can serialize (<see cref="IsSerializableType"/>).
    /// Properties never serialize; a renamed field keeps reading its [FormerlySerializedAs] names.
    ///
    /// The engine's hand-written built-ins (CosmicShore.Engine) and the re-implemented packages
    /// (CosmicShore.Compat) mirror Unity's native layouts through m_ aliases and properties, so
    /// their members are not judged by these rules.
    /// </summary>
    public static class UnitySerializationRules
    {
        /// <summary>True when <paramref name="declaring"/> is script code, not an engine or package built-in.</summary>
        public static bool IsScriptType(Type declaring)
        {
            var name = declaring.Assembly.GetName().Name;
            return name is not ("CosmicShore.Engine" or "CosmicShore.Compat") && !name!.StartsWith("System", StringComparison.Ordinal)
                   && name != "netstandard" && name != "mscorlib";
        }

        public static bool IsBackingField(FieldInfo f) => f.Name.StartsWith("<", StringComparison.Ordinal) && f.Name.EndsWith(">k__BackingField", StringComparison.Ordinal);

        /// <summary>Unity's rule for one field of a script type.</summary>
        public static bool IsSerialized(FieldInfo f)
        {
            if (f.IsStatic || f.IsInitOnly || f.IsLiteral) return false;
            if (f.IsDefined(typeof(NonSerializedAttribute), false)) return false;
            if (f.IsDefined(typeof(SerializeReference), false)) return true;
            bool marked = f.IsDefined(typeof(SerializeField), false);
            if (IsBackingField(f)) return marked && IsSerializableType(f.FieldType); // [field: SerializeField] on an auto-property
            if (f.Name.Contains('<')) return false;          // other compiler-generated fields
            return (f.IsPublic || marked) && IsSerializableType(f.FieldType);
        }

        /// <summary>
        /// Whether Unity can serialize a field of this type: primitives, enums, strings, Unity object
        /// references, the engine's built-in value types, [Serializable] script classes and structs
        /// (generic ones included), and one-dimensional arrays or List&lt;T&gt; of those - but not a
        /// list of lists, an interface or abstract type (that needs [SerializeReference]), or a .NET
        /// collection such as Dictionary.
        /// </summary>
        public static bool IsSerializableType(Type t) => IsSerializableType(t, allowCollection: true);

        static bool IsSerializableType(Type t, bool allowCollection)
        {
            if (t.IsArray)
                return allowCollection && t.GetArrayRank() == 1 && IsSerializableType(t.GetElementType()!, allowCollection: false);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                return allowCollection && IsSerializableType(t.GetGenericArguments()[0], allowCollection: false);
            if (t.IsPrimitive) return t != typeof(IntPtr) && t != typeof(UIntPtr);
            if (t.IsEnum || t == typeof(string)) return true;
            if (typeof(CosmicShore.Engine.Object).IsAssignableFrom(t)) return true;  // a reference to a Unity object
            if (t.IsInterface || t.IsAbstract || t.IsPointer || t.IsByRef) return false;
            var asm = t.Assembly.GetName().Name ?? "";
            if (asm.StartsWith("System", StringComparison.Ordinal) || asm is "netstandard" or "mscorlib")
                return false;                                                          // decimal, DateTime, Dictionary...
            if (!IsScriptType(t)) return true;                                         // engine built-ins: Vector3, Color, UnityEvent...
            var def = t.IsGenericType ? t.GetGenericTypeDefinition() : t;
            return (def.Attributes & TypeAttributes.Serializable) != 0;              // [Serializable] (a metadata flag, not a custom attribute)
        }

        /// <summary>
        /// For a key in a script type's YAML body: the field Unity reads it into (by name, or by a
        /// [FormerlySerializedAs] name), or null when Unity ignores the key. Fields declared on
        /// built-in base types are reported through <paramref name="builtIn"/> instead.
        /// </summary>
        public static FieldInfo? UnityField(Type type, string key, out bool builtIn)
        {
            builtIn = false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                if (!IsScriptType(t))
                {
                    // A built-in base (MonoBehaviour, Graphic, Selectable...): its own keys are native.
                    foreach (var f in t.GetFields(flags)) if (f.Name == key) { builtIn = true; return f; }
                    continue;
                }
                foreach (var f in t.GetFields(flags))
                {
                    if (!IsSerialized(f)) continue;
                    if (f.Name == key) return f;
                    foreach (var fs in f.GetCustomAttributes<FormerlySerializedAsAttribute>(false))
                        if (fs.oldName == key) return f;
                }
            }
            return null;
        }

        /// <summary>Every field Unity serializes for a script type, most-derived first.</summary>
        public static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                if (IsScriptType(t))
                    foreach (var f in t.GetFields(flags))
                        if (IsSerialized(f)) yield return f;
        }
    }
}
