using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace CosmicShore.Engine.Networking
{
    /// <summary>Marks a server-executed RPC. Local-invoke semantics until the transport phase.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ServerRpcAttribute : Attribute { public bool RequireOwnership = true; }

    /// <summary>Marks a client-executed RPC. Local-invoke semantics until the transport phase.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ClientRpcAttribute : Attribute { }
}

namespace CosmicShore.Engine
{
    public enum ScreenOrientation { Unknown = 0, Portrait = 1, PortraitUpsideDown = 2, LandscapeLeft = 3, LandscapeRight = 4, AutoRotation = 5 }
    public enum DeviceType { Unknown = 0, Handheld = 1, Console = 2, Desktop = 3 }
    public enum RuntimePlatform { WindowsPlayer = 2, OSXPlayer = 1, LinuxPlayer = 13, Android = 11, IPhonePlayer = 8 }

    public struct Resolution
    {
        public int width;
        public int height;
        public int refreshRate;
    }

    public static class Screen
    {
        public static int width = 1280;
        public static int height = 720;
        public static float dpi = 96f;
        public static ScreenOrientation orientation = ScreenOrientation.LandscapeLeft;
        public static bool sleepTimeout;
        public static bool autorotateToPortrait;
        public static bool autorotateToPortraitUpsideDown;
        public static bool autorotateToLandscapeLeft;
        public static bool autorotateToLandscapeRight;
        public static Resolution currentResolution => new() { width = width, height = height, refreshRate = 60 };
    }

    public static class SystemInfo
    {
        public static DeviceType deviceType = DeviceType.Desktop;
    }

    public static class Application
    {
        public static bool isPlaying = true;
        public static bool isMobilePlatform => platform is RuntimePlatform.Android or RuntimePlatform.IPhonePlayer;
        public static RuntimePlatform platform =
            OperatingSystem.IsWindows() ? RuntimePlatform.WindowsPlayer :
            OperatingSystem.IsMacOS() ? RuntimePlatform.OSXPlayer : RuntimePlatform.LinuxPlayer;
        public static int targetFrameRate = -1;
        public static string version = "0.2.0-port";
        public static string persistentDataPath
        {
            get
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CosmicShore");
                Directory.CreateDirectory(path);
                return path;
            }
        }
        public static event Action quitting;
        public static void Quit() => quitting?.Invoke();
    }

    /// <summary>In-memory prefs with JSON persistence under persistentDataPath.</summary>
    public static class PlayerPrefs
    {
        static Dictionary<string, object> _values;
        static string FilePath => Path.Combine(Application.persistentDataPath, "prefs.json");

        static Dictionary<string, object> Values
        {
            get
            {
                if (_values != null) return _values;
                _values = new Dictionary<string, object>();
                try
                {
                    if (File.Exists(FilePath))
                        foreach (var kv in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(FilePath)))
                            _values[kv.Key] = kv.Value.ValueKind switch
                            {
                                JsonValueKind.Number when kv.Value.TryGetInt32(out int i) => i,
                                JsonValueKind.Number => (float)kv.Value.GetDouble(),
                                _ => kv.Value.GetString(),
                            };
                }
                catch (Exception e) { Debug.LogWarning($"PlayerPrefs load failed: {e.Message}"); }
                return _values;
            }
        }

        public static int GetInt(string key, int defaultValue = 0) => Values.TryGetValue(key, out var v) && v is int i ? i : defaultValue;
        public static float GetFloat(string key, float defaultValue = 0f) => Values.TryGetValue(key, out var v) ? v switch { float f => f, int i => i, _ => defaultValue } : defaultValue;
        public static string GetString(string key, string defaultValue = "") => Values.TryGetValue(key, out var v) && v is string s ? s : defaultValue;
        public static void SetInt(string key, int value) => Values[key] = value;
        public static void SetFloat(string key, float value) => Values[key] = value;
        public static void SetString(string key, string value) => Values[key] = value;
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static void DeleteKey(string key) => Values.Remove(key);
        public static void DeleteAll() => Values.Clear();

        public static void Save()
        {
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(Values)); }
            catch (Exception e) { Debug.LogWarning($"PlayerPrefs save failed: {e.Message}"); }
        }
    }

    /// <summary>Asset lookups. Backed by an explicit registry until the content phase wires real loading.</summary>
    public static class Resources
    {
        static readonly List<ScriptableObject> Registry = new();
        static readonly Dictionary<string, Object> PathRegistry = new();

        public static void Register(ScriptableObject asset) { if (!Registry.Contains(asset)) Registry.Add(asset); }

        /// <summary>Registers an asset at a Resources-relative path for <see cref="Load{T}"/>.</summary>
        public static void Register(string path, Object asset) => PathRegistry[path] = asset;

        public static void Clear() { Registry.Clear(); PathRegistry.Clear(); }

        /// <summary>
        /// Original engine contract: returns the asset registered at the Resources-relative
        /// path, or null when nothing (or a different type) is registered there.
        /// </summary>
        public static T Load<T>(string path) where T : Object
            => PathRegistry.TryGetValue(path, out var asset) ? asset as T : null;

        public static T[] FindObjectsOfTypeAll<T>() where T : ScriptableObject
        {
            var results = new List<T>();
            foreach (var asset in Registry)
                if (asset is T match) results.Add(match);
            return results.ToArray();
        }
    }

    public enum CursorLockMode { None = 0, Locked = 1, Confined = 2 }

    /// <summary>Cursor state holder; the windowing backend applies it (phase 5).</summary>
    public static class Cursor
    {
        public static bool visible = true;
        public static CursorLockMode lockState = CursorLockMode.None;
    }

    public struct RaycastHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public Collider collider;
    }

    /// <summary>Collision queries return no hits until the physics design lands (phase 2).</summary>
    public static class Physics
    {
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity)
        { hitInfo = default; return false; }
        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance = float.PositiveInfinity) => false;

        /// <summary>
        /// Original-contract sphere query against every registered collider (trigger and
        /// non-trigger), backed by the loop's collider registry. Deterministic
        /// registration-order results, truncated at the buffer length.
        /// </summary>
        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results)
            => GameLoop.Current?.Triggers.OverlapSphereNonAlloc(position, radius, results) ?? 0;
    }

    /// <summary>
    /// Kinematic placeholder. The port's trigger physics needs no rigidbodies (see
    /// TriggerPass); fields hold authored values so [RequireComponent(typeof(Rigidbody))]
    /// components and their setup code port verbatim. Dynamics arrive with the full
    /// physics phase.
    /// </summary>
    public class Rigidbody : Component
    {
        public bool isKinematic = true;
        public bool useGravity;
        public float mass = 1f;
        public Vector3 velocity;
    }

    public class Collider : Behaviour
    {
        public bool isTrigger;
        public virtual Vector3 ClosestPoint(Vector3 position) => transform.position;

        internal override void DestroyComponentNow()
        {
            // Leave the trigger-pass registry; pairs still tracking this collider fire
            // OnTriggerExit on the surviving side next pass.
            GameLoop.Current?.Triggers.Unregister(this);
            base.DestroyComponentNow();
        }
    }

    public class BoxCollider : Collider
    {
        public Vector3 center = Vector3.zero;
        public Vector3 size = Vector3.one;
    }

    public class SphereCollider : Collider
    {
        public Vector3 center = Vector3.zero;
        public float radius = 0.5f;
    }

    // E7/E8: Object statics that ported code calls.
    public static class ObjectUtilities
    {
        /// <summary>Single-scene engine: nothing is destroyed on (nonexistent) scene loads.</summary>
        public static void DontDestroyOnLoad(Object target) { }

        public static T FindFirstObjectByType<T>() where T : class
            => GameLoop.Current?.Scene.FindObjectOfType<T>(includeInactive: true);

        /// <summary>
        /// Clone an asset or object graph. ScriptableObjects shallow-clone (the AIPilot
        /// profile path); GameObjects/Components clone structurally with field copies
        /// (the pool path). Full prefab factories arrive with the content phase.
        /// </summary>
        public static T InstantiateObject<T>(T original) where T : Object
        {
            switch (original)
            {
                case ScriptableObject so:
                {
                    var clone = (ScriptableObject)CloneViaMemberwise(so);
                    clone.name = so.name + "(Clone)";
                    return clone as T;
                }
                case GameObject go:
                    return CloneGameObject(go) as T;
                case Component component:
                {
                    var cloned = CloneGameObject(component.gameObject);
                    return cloned.GetComponent(component.GetType()) as T;
                }
                default:
                    throw new InvalidOperationException($"Instantiate: unsupported type {original.GetType().Name}");
            }
        }

        static readonly MethodInfo MemberwiseCloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        static object CloneViaMemberwise(object source) => MemberwiseCloneMethod.Invoke(source, null);

        /// <summary>
        /// E16: prefab-faithful clone. Structural copy first (building old→new maps for
        /// every GameObject/Transform/Component in the source tree), then a remap pass
        /// rewrites cloned serialized fields — and array/<see cref="List{T}"/> elements —
        /// that point INSIDE the source tree to their clone counterparts, matching the
        /// original engine's Instantiate. References outside the tree (other scene
        /// objects, ScriptableObject assets) are left untouched. Plain [Serializable]
        /// class graphs (e.g. ResourceSystem.Resources' <c>List&lt;Resource&gt;</c>) are
        /// deep-cloned the way the original engine's serializer inlines them — see
        /// <see cref="RemapValue"/> for the full value rules. Only the root gains the
        /// "(Clone)" suffix; children keep their authored names.
        /// </summary>
        static GameObject CloneGameObject(GameObject source)
        {
            var map = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
            var clone = CloneHierarchy(source, map, isRoot: true);
            RemapClonedReferences(map);
            return clone;
        }

        static GameObject CloneHierarchy(GameObject source, Dictionary<object, object> map, bool isRoot)
        {
            var clone = new GameObject(isRoot ? source.name + "(Clone)" : source.name);
            map[source] = clone;
            map[source.transform] = clone.transform;

            clone.layer = source.layer;
            clone.tag = source.tag;
            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localRotation = source.transform.localRotation;
            clone.transform.localScale = source.transform.localScale;
            if (!source.activeSelf) clone.SetActive(false);

            foreach (var component in source.Components)
            {
                if (component is Transform) continue;
                var copy = clone.AddComponent(component.GetType());
                CopyFields(component, copy);
                map[component] = copy;
            }

            foreach (var child in source.transform.Children)
            {
                var childClone = CloneHierarchy(child.gameObject, map, isRoot: false);
                childClone.transform.SetParent(clone.transform, worldPositionStays: false);
            }
            return clone;
        }

        static void CopyFields(object source, object target)
        {
            foreach (var field in SerializedFieldCandidates(source.GetType()))
                field.SetValue(target, field.GetValue(source));
        }

        /// <summary>
        /// The field set Instantiate copies/remaps: every declared instance field up to —
        /// but excluding — the engine base classes. NetworkBehaviour state (spawn flags,
        /// NetworkObjectId, the NetworkObject handle) is engine infrastructure, never
        /// cloned: a fresh instance starts unspawned, exactly like the original engine.
        /// </summary>
        static IEnumerable<FieldInfo> SerializedFieldCandidates(Type type)
        {
            for (Type t = type;
                 t != null && t != typeof(Component) && t != typeof(Behaviour) && t != typeof(MonoBehaviour)
                 && t != typeof(Networking.NetworkBehaviour);
                 t = t.BaseType)
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    yield return field;
        }

        /// <summary>E16 remap pass: rewrite intra-tree references on every cloned component.</summary>
        static void RemapClonedReferences(Dictionary<object, object> map)
        {
            foreach (var cloned in map.Values)
            {
                if (cloned is not Component component || component is Transform) continue;
                foreach (var field in SerializedFieldCandidates(component.GetType()))
                    RemapField(component, field, map);
            }
        }

        /// <summary>
        /// The original engine's serialization depth limit for nested plain
        /// ([Serializable]) classes. A plain-object reference nested deeper than this is
        /// dropped to null, exactly like the engine truncating "Serialization depth limit
        /// 10 exceeded" graphs. This doubles as the cycle guard: per-field independent
        /// copies of a cyclic plain graph terminate here instead of recursing forever.
        /// </summary>
        const int MaxPlainObjectDepth = 10;

        static void RemapField(object target, FieldInfo field, Dictionary<object, object> map)
        {
            // Value types (and therefore primitives/enums/structs) can't hold scene
            // references and were already value-copied by CopyFields.
            if (field.FieldType.IsValueType) return;

            object value = field.GetValue(target);
            if (value is null) return;

            object replacement = RemapValue(value, map, depth: 0);
            if (!ReferenceEquals(replacement, value))
                field.SetValue(target, replacement);
        }

        /// <summary>
        /// E16 value rules, applied uniformly to component fields, collection elements, and
        /// the fields of deep-cloned plain objects:
        ///   • engine Object inside the source tree → its clone counterpart;
        ///   • engine Object outside the tree (other scene objects, ScriptableObject
        ///     assets) → kept, shared by design;
        ///   • string → kept (immutable);
        ///   • rank-1 array / <see cref="List{T}"/> → NEW container, elements rerun through
        ///     these same rules (the original engine never shares a collection instance
        ///     between a template and its clone, whatever the element type);
        ///   • plain [Serializable] class → independent deep clone per field path. The
        ///     original engine inlines plain classes BY VALUE, so aliasing within the
        ///     template's plain-object graph intentionally does not survive cloning; graphs
        ///     truncate to null past <see cref="MaxPlainObjectDepth"/>;
        ///   • everything else (delegate fields on components, dictionaries, framework
        ///     types, multidimensional arrays) → reference copy — the port's existing
        ///     semantics for shapes the original engine never serialized.
        /// <paramref name="depth"/> counts plain-object nesting levels already entered;
        /// collection containers are transparent to it.
        /// </summary>
        static object RemapValue(object value, Dictionary<object, object> map, int depth)
        {
            // Direct reference into the source tree (declared type may be an interface —
            // the runtime value is what's checked).
            if (map.TryGetValue(value, out var mapped))
                return mapped;

            switch (value)
            {
                // Engine reference outside the cloned tree: scene objects and
                // ScriptableObject assets are shared by design — keep.
                case Object:
                    return value;

                case string:
                    return value;

                case Array array when array.Rank == 1:
                    return CloneArrayValue(array, map, depth);

                case IList list when value.GetType() is { IsGenericType: true } listType
                                     && listType.GetGenericTypeDefinition() == typeof(List<>):
                    return CloneListValue(list, listType, map, depth);

                default:
                    if (!IsInlinePlainClass(value.GetType()))
                        return value;
                    if (depth >= MaxPlainObjectDepth)
                        return null; // depth-10 truncation — also the cycle guard
                    return ClonePlainObject(value, map, depth + 1);
            }
        }

        static Array CloneArrayValue(Array array, Dictionary<object, object> map, int depth)
        {
            Type elementType = array.GetType().GetElementType();

            // Value-type/string elements can't reference the tree — a shallow clone IS the value copy.
            if (elementType.IsValueType || elementType == typeof(string))
                return (Array)array.Clone();

            var copy = Array.CreateInstance(elementType, array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                object element = array.GetValue(i);
                copy.SetValue(element is null ? null : RemapValue(element, map, depth), i);
            }
            return copy;
        }

        static IList CloneListValue(IList list, Type listType, Dictionary<object, object> map, int depth)
        {
            Type elementType = listType.GetGenericArguments()[0];

            // Value-type/string elements can't reference the tree — List<T>(IEnumerable<T>) IS the value copy.
            if (elementType.IsValueType || elementType == typeof(string))
                return (IList)Activator.CreateInstance(listType, list);

            var copy = (IList)Activator.CreateInstance(listType, list.Count);
            foreach (var element in list)
                copy.Add(element is null ? null : RemapValue(element, map, depth));
            return copy;
        }

        /// <summary>
        /// A class the original engine would serialize INLINE (by value) inside a
        /// component: marked [Serializable], not an engine Object (those serialize as
        /// references), not a delegate (never serialized), and not a framework type (the
        /// engine doesn't inline System.* shapes like Dictionary — the port keeps its
        /// existing reference-copy behavior for them). Strings, arrays, and
        /// <see cref="List{T}"/> are handled before this check.
        /// </summary>
        static bool IsInlinePlainClass(Type type) =>
            type.IsClass
            && type.IsDefined(typeof(SerializableAttribute), inherit: false) // [Serializable], sans the obsolete IsSerializable/flag APIs
            && !typeof(Object).IsAssignableFrom(type)
            && !typeof(Delegate).IsAssignableFrom(type)
            && (type.Namespace is not { } ns
                || (ns != "System" && !ns.StartsWith("System.", StringComparison.Ordinal)));

        /// <summary>
        /// Deep clone of a plain [Serializable] object, engine-inline style: memberwise
        /// copy first (value fields — including non-serialized ones — keep the port's
        /// copy-everything pragmatics, same deal as <see cref="SerializedFieldCandidates"/>),
        /// then every reference field is rerun through <see cref="RemapValue"/>.
        /// Delegate/event fields reset to null: the original engine never serializes
        /// runtime wiring, and carrying it over would aim the TEMPLATE's subscribers at
        /// the clone's state.
        /// </summary>
        static object ClonePlainObject(object source, Dictionary<object, object> map, int depth)
        {
            object clone = CloneViaMemberwise(source);
            for (Type t = source.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType.IsValueType) continue; // memberwise copy already value-copied it

                    if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                    {
                        field.SetValue(clone, null);
                        continue;
                    }

                    object value = field.GetValue(clone);
                    if (value is null) continue;

                    object replacement = RemapValue(value, map, depth);
                    if (!ReferenceEquals(replacement, value))
                        field.SetValue(clone, replacement);
                }
            }
            return clone;
        }
    }
}
