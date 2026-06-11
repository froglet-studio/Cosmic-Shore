using System;
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

    public static class Screen
    {
        public static int width = 1280;
        public static int height = 720;
        public static ScreenOrientation orientation = ScreenOrientation.LandscapeLeft;
        public static bool sleepTimeout;
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

        public static void Register(ScriptableObject asset) { if (!Registry.Contains(asset)) Registry.Add(asset); }
        public static void Clear() => Registry.Clear();

        public static T[] FindObjectsOfTypeAll<T>() where T : ScriptableObject
        {
            var results = new List<T>();
            foreach (var asset in Registry)
                if (asset is T match) results.Add(match);
            return results.ToArray();
        }
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
    }

    public class Collider : Behaviour
    {
        public bool isTrigger;
        public virtual Vector3 ClosestPoint(Vector3 position) => transform.position;
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

        static GameObject CloneGameObject(GameObject source)
        {
            var clone = new GameObject(source.name + "(Clone)");
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
            }

            foreach (var child in source.transform.Children)
            {
                var childClone = CloneGameObject(child.gameObject);
                childClone.transform.SetParent(clone.transform, worldPositionStays: false);
            }
            return clone;
        }

        static void CopyFields(object source, object target)
        {
            for (Type t = source.GetType(); t != null && t != typeof(Component) && t != typeof(Behaviour) && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    field.SetValue(target, field.GetValue(source));
        }
    }
}
