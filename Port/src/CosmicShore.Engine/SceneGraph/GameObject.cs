using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Scene entity: a named, activatable container of components with a Transform.
    /// Creating one requires an active <see cref="GameLoop"/> (fail loud — no hidden
    /// global fallbacks).
    /// </summary>
    public sealed class GameObject : Object
    {
        readonly List<Component> _components = new();

        public Transform transform { get; }

        /// <summary>
        /// Self-reference, matching the original engine's GameObject.gameObject property —
        /// ported call sites occasionally chain it (e.g. VesselStatus's
        /// <c>gameObject.gameObject.GetOrAdd&lt;AIPilot&gt;()</c>).
        /// </summary>
        public GameObject gameObject => this;

        public Scene scene { get; internal set; }
        public int layer;
        public string tag = "Untagged";

        public bool activeSelf { get; private set; } = true;

        public GameObject(string name = "GameObject")
        {
            var loop = GameLoop.Current
                ?? throw new InvalidOperationException(
                    "Creating a GameObject requires an active GameLoop. Construct a GameLoop first.");

            base.name = name;
            transform = new Transform { gameObject = this };
            _components.Add(transform);
            scene = loop.Scene;
            scene.AddRoot(this);
        }

        public override string name { get => base.name; set => base.name = value; }

        public bool activeInHierarchy
        {
            get
            {
                if (destroyedFlag) return false;
                for (Transform t = transform; t is not null; t = t.parent)
                    if (!t.gameObject.activeSelf) return false;
                return true;
            }
        }

        public void SetActive(bool value)
        {
            if (activeSelf == value || destroyedFlag) return;

            bool parentActive = transform.parent is null || transform.parent.gameObject.activeInHierarchy;
            activeSelf = value;

            // Effective state only changes when every ancestor is active.
            if (parentActive) NotifyHierarchyActiveChanged(value);
        }

        /// <summary>Propagate an effective-activation change to this subtree's behaviours.</summary>
        internal void NotifyHierarchyActiveChanged(bool active)
        {
            // Snapshot: callbacks may mutate the component list.
            var components = _components.ToArray();
            foreach (var component in components)
                if (component is MonoBehaviour mb)
                    mb.HandleHierarchyActive(active);

            foreach (var child in transform.Children)
                if (child.gameObject.activeSelf)
                    child.gameObject.NotifyHierarchyActiveChanged(active);
        }

        // ── Components ───────────────────────────────────────────────

        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));

        public Component AddComponent(Type componentType)
        {
            if (!typeof(Component).IsAssignableFrom(componentType))
                throw new ArgumentException($"{componentType.Name} is not a Component.");
            var component = (Component)Activator.CreateInstance(componentType, nonPublic: true);
            component.gameObject = this;
            _components.Add(component);
            if (component is Collider collider)
                GameLoop.Current.Triggers.Register(collider); // trigger-pass registry (creation order)
            if (component is MonoBehaviour mb)
            {
                mb.sequence = GameLoop.Current.NextSequence();
                mb.HandleAttached();
            }
            return component;
        }

        public Component GetComponent(Type type)
        {
            foreach (var component in _components)
                if (type.IsInstanceOfType(component) && !component.destroyedFlag)
                    return component;
            return null;
        }

        public T GetComponent<T>() where T : class
        {
            foreach (var component in _components)
                if (component is T match && !component.destroyedFlag)
                    return match;
            return null;
        }

        public bool TryGetComponent<T>(out T component) where T : class
        {
            component = GetComponent<T>();
            return component != null;
        }

        public T[] GetComponents<T>() where T : class
        {
            var results = new List<T>();
            foreach (var component in _components)
                if (component is T match && !component.destroyedFlag)
                    results.Add(match);
            return results.ToArray();
        }

        public T GetComponentInChildren<T>(bool includeInactive = false) where T : class
        {
            if (includeInactive || activeInHierarchy)
            {
                var own = GetComponent<T>();
                if (own != null) return own;
            }
            foreach (var child in transform.Children)
            {
                var found = child.gameObject.GetComponentInChildren<T>(includeInactive);
                if (found != null) return found;
            }
            return null;
        }

        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class
        {
            var results = new List<T>();
            CollectComponentsInChildren(results, includeInactive);
            return results.ToArray(); // original-engine contract: array result (callers index by .Length)
        }

        void CollectComponentsInChildren<T>(List<T> results, bool includeInactive) where T : class
        {
            if (includeInactive || activeInHierarchy)
                results.AddRange(GetComponents<T>());
            foreach (var child in transform.Children)
                child.gameObject.CollectComponentsInChildren(results, includeInactive);
        }

        public T GetComponentInParent<T>(bool includeInactive = false) where T : class
        {
            for (Transform t = transform; t is not null; t = t.parent)
            {
                if (!includeInactive && !t.gameObject.activeInHierarchy) continue;
                var found = t.gameObject.GetComponent<T>();
                if (found != null) return found;
            }
            return null;
        }

        internal void RemoveComponentInternal(Component component) => _components.Remove(component);

        internal IReadOnlyList<Component> Components => _components;

        // ── Destruction ──────────────────────────────────────────────

        /// <summary>Immediate recursive destruction (children first, then components).</summary>
        internal void DestroyNow()
        {
            if (destroyedFlag) return;

            // Children first (snapshot — destruction mutates the list).
            var children = new List<Transform>(transform.Children);
            foreach (var child in children)
                child.gameObject.DestroyNow();

            // Components: behaviours get OnDisable/OnDestroy.
            var components = _components.ToArray();
            foreach (var component in components)
                component.DestroyComponentNow();
            _components.Clear();

            if (transform.parent is null) scene?.RemoveRoot(this);
            else transform.SetParentForDestroy();

            destroyedFlag = true;
        }
    }
}
