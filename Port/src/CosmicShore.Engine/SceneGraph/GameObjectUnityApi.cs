using System;
using System.Collections.Generic;
using System.Reflection;

namespace CosmicShore.Engine
{
    public enum SendMessageOptions { RequireReceiver = 0, DontRequireReceiver = 1 }

    /// <summary>The remainder of the original GameObject surface (queries, messaging, tags).</summary>
    public sealed partial class GameObject
    {
        public bool CompareTag(string tagName) => string.Equals(tag, tagName, StringComparison.Ordinal);

        public T GetComponent<T>(bool unused) where T : class => GetComponent<T>();

        public Component GetComponent(string typeName)
        {
            foreach (var c in _components)
                if (!c.destroyedFlag && c.GetType().Name == typeName) return c;
            return null;
        }

        public bool TryGetComponent(Type type, out Component component) { component = GetComponent(type); return component != null; }

        public void GetComponents<T>(List<T> results) where T : class
        {
            results.Clear();
            foreach (var c in _components) if (c is T m && !c.destroyedFlag) results.Add(m);
        }

        public Component[] GetComponents(Type type)
        {
            var r = new List<Component>();
            foreach (var c in _components) if (type.IsInstanceOfType(c) && !c.destroyedFlag) r.Add(c);
            return r.ToArray();
        }

        public void GetComponentsInChildren<T>(List<T> results) where T : class => GetComponentsInChildren(false, results);

        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) where T : class
        {
            results.Clear();
            results.AddRange(GetComponentsInChildren<T>(includeInactive));
        }

        public Component GetComponentInChildren(Type type, bool includeInactive = false)
        {
            if (includeInactive || activeInHierarchy)
            {
                var own = GetComponent(type);
                if (own != null) return own;
            }
            foreach (var child in transform.Children)
            {
                var f = child.gameObject.GetComponentInChildren(type, includeInactive);
                if (f != null) return f;
            }
            return null;
        }

        public Component[] GetComponentsInChildren(Type type, bool includeInactive = false)
        {
            var r = new List<Component>();
            void Walk(GameObject g)
            {
                if (includeInactive || g.activeInHierarchy) r.AddRange(g.GetComponents(type));
                foreach (var ch in g.transform.Children) Walk(ch.gameObject);
            }
            Walk(this);
            return r.ToArray();
        }

        public Component GetComponentInParent(Type type, bool includeInactive = false)
        {
            for (Transform t = transform; t is not null; t = t.parent)
            {
                if (!includeInactive && !t.gameObject.activeInHierarchy) continue;
                var f = t.gameObject.GetComponent(type);
                if (f != null) return f;
            }
            return null;
        }

        public T[] GetComponentsInParent<T>(bool includeInactive = false) where T : class
        {
            var r = new List<T>();
            for (Transform t = transform; t is not null; t = t.parent)
                if (includeInactive || t.gameObject.activeInHierarchy) r.AddRange(t.gameObject.GetComponents<T>());
            return r.ToArray();
        }

        public void GetComponentsInParent<T>(bool includeInactive, List<T> results) where T : class
        {
            results.Clear();
            results.AddRange(GetComponentsInParent<T>(includeInactive));
        }

        public int GetComponentCount() => _components.Count;
        public Component GetComponentAtIndex(int index) => _components[index];
        public int GetComponentIndex(Component component) => _components.IndexOf(component);

        // ── messaging ─────────────────────────────────────────────────────────

        static readonly Dictionary<(Type, string), MethodInfo> s_MessageCache = new();

        static MethodInfo FindMessage(Type t, string name)
        {
            if (s_MessageCache.TryGetValue((t, name), out var m)) return m;
            for (var tt = t; tt != null && m == null; tt = tt.BaseType)
                foreach (var mi in tt.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (mi.Name == name && mi.GetParameters().Length <= 1) { m = mi; break; }
            return s_MessageCache[(t, name)] = m;
        }

        /// <summary>Calls <paramref name="methodName"/> on every enabled MonoBehaviour of this object.</summary>
        public void SendMessage(string methodName, object value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
        {
            bool received = false;
            foreach (var c in _components.ToArray())
            {
                if (c is not MonoBehaviour mb || c.destroyedFlag || !mb.isActiveAndEnabled && !mb.enabled) continue;
                var m = FindMessage(c.GetType(), methodName);
                if (m == null) continue;
                received = true;
                try { m.Invoke(c, m.GetParameters().Length == 0 ? null : new[] { value }); }
                catch (TargetInvocationException e) { Debug.LogException(e.InnerException ?? e); }
            }
            if (!received && options == SendMessageOptions.RequireReceiver)
                Debug.LogError($"SendMessage {methodName} has no receiver!");
        }

        public void SendMessage(string methodName, SendMessageOptions options) => SendMessage(methodName, null, options);

        public void SendMessageUpwards(string methodName, object value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
        {
            for (Transform t = transform; t is not null; t = t.parent)
                t.gameObject.SendMessage(methodName, value, SendMessageOptions.DontRequireReceiver);
        }

        public void BroadcastMessage(string methodName, object value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver)
        {
            SendMessage(methodName, value, SendMessageOptions.DontRequireReceiver);
            foreach (var child in new List<Transform>(transform.Children))
                child.gameObject.BroadcastMessage(methodName, value, SendMessageOptions.DontRequireReceiver);
        }

        public void BroadcastMessage(string methodName, SendMessageOptions options) => BroadcastMessage(methodName, null, options);

        // ── scene-wide queries ───────────────────────────────────────────────

        static IEnumerable<GameObject> AllLoaded()
        {
            var loop = GameLoop.Current;
            if (loop == null) yield break;
            var stack = new Stack<Transform>();
            foreach (var root in loop.Scene.GetRootGameObjects()) stack.Push(root.transform);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                yield return t.gameObject;
                foreach (var c in t.Children) stack.Push(c);
            }
        }

        /// <summary>First ACTIVE object with this name, or a path "Parent/Child" from a root.</summary>
        public static GameObject Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (name.Contains('/'))
            {
                var parts = name.TrimStart('/').Split('/');
                var loop = GameLoop.Current;
                if (loop == null) return null;
                foreach (var root in loop.Scene.GetRootGameObjects())
                {
                    if (root.name != parts[0] || !root.activeInHierarchy) continue;
                    var t = root.transform;
                    for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                    if (t != null && t.gameObject.activeInHierarchy) return t.gameObject;
                }
                return null;
            }
            foreach (var go in AllLoaded()) if (go.name == name && go.activeInHierarchy) return go;
            return null;
        }

        public static GameObject FindWithTag(string tag) => FindGameObjectWithTag(tag);

        public static GameObject FindGameObjectWithTag(string tag)
        {
            foreach (var go in AllLoaded()) if (go.activeInHierarchy && go.tag == tag) return go;
            return null;
        }

        public static GameObject[] FindGameObjectsWithTag(string tag)
        {
            var r = new List<GameObject>();
            foreach (var go in AllLoaded()) if (go.activeInHierarchy && go.tag == tag) r.Add(go);
            return r.ToArray();
        }
    }

    public abstract partial class Component
    {
        public string tag { get => gameObject.tag; set => gameObject.tag = value; }
        public bool CompareTag(string tag) => gameObject.CompareTag(tag);
        public Component GetComponent(Type type) => gameObject.GetComponent(type);
        public Component GetComponent(string type) => gameObject.GetComponent(type);
        public bool TryGetComponent(Type type, out Component component) => gameObject.TryGetComponent(type, out component);
        public Component[] GetComponents(Type type) => gameObject.GetComponents(type);
        public void GetComponents<T>(List<T> results) where T : class => gameObject.GetComponents(results);
        public void GetComponentsInChildren<T>(List<T> results) where T : class => gameObject.GetComponentsInChildren(results);
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) where T : class => gameObject.GetComponentsInChildren(includeInactive, results);
        public Component GetComponentInChildren(Type t, bool includeInactive = false) => gameObject.GetComponentInChildren(t, includeInactive);
        public Component[] GetComponentsInChildren(Type t, bool includeInactive = false) => gameObject.GetComponentsInChildren(t, includeInactive);
        public Component GetComponentInParent(Type t, bool includeInactive = false) => gameObject.GetComponentInParent(t, includeInactive);
        public T[] GetComponentsInParent<T>(bool includeInactive = false) where T : class => gameObject.GetComponentsInParent<T>(includeInactive);
        public void GetComponentsInParent<T>(bool includeInactive, List<T> results) where T : class => gameObject.GetComponentsInParent(includeInactive, results);
        public void SendMessage(string methodName, object value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver) => gameObject.SendMessage(methodName, value, options);
        public void SendMessage(string methodName, SendMessageOptions options) => gameObject.SendMessage(methodName, options);
        public void SendMessageUpwards(string methodName, object value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver) => gameObject.SendMessageUpwards(methodName, value, options);
        public void BroadcastMessage(string methodName, object value = null, SendMessageOptions options = SendMessageOptions.RequireReceiver) => gameObject.BroadcastMessage(methodName, value, options);
    }
}
