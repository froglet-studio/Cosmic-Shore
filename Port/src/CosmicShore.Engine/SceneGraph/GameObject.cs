using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Scene entity: a named, activatable container of components with a Transform.
    /// Creating one requires an active <see cref="GameLoop"/> (fail loud — no hidden
    /// global fallbacks).
    /// </summary>
    public sealed partial class GameObject : Object
    {
        readonly List<Component> _components = new();

        public Transform transform { get; private set; }

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

        /// <summary>Survives Single scene loads (set via <see cref="Object.DontDestroyOnLoad"/> on a root).</summary>
        internal bool dontDestroyOnLoad;

        /// <summary>
        /// True for a prefab ASSET (the content loader's inactive templates): it lives outside
        /// every scene, so Find*/FindObjectsByType never return it — Unity's contract.
        /// </summary>
        public bool isPrefabAsset { get; private set; }

        /// <summary>Marks this root as a prefab asset (content loader only).</summary>
        public void MarkAsPrefabAsset() => isPrefabAsset = true;
        public bool IsDontDestroyOnLoad => dontDestroyOnLoad;

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

        /// <summary>
        /// Original-engine contract: create with components attached in order. A
        /// Transform-derived type in the list (RectTransform) becomes the object's
        /// transform (the UI creation idiom <c>new GameObject("x", typeof(RectTransform))</c>).
        /// </summary>
        public GameObject(string name, params Type[] components) : this(name)
        {
            foreach (var type in components)
                AddComponent(type);
        }

        /// <summary>
        /// Original-engine contract: a GameObject named after the primitive with a
        /// MeshFilter holding the shared built-in primitive mesh (see
        /// <see cref="PrimitiveMeshes"/> — real mesh data since the mesh arc), a
        /// MeshRenderer, and a matching collider. Collider shapes: Sphere gets a
        /// radius-0.5 SphereCollider; every other shape gets a BoxCollider sized to its
        /// mesh bounds (Cube = unit box, Capsule/Cylinder = (1, 2, 1), Plane/Quad = flat)
        /// — box approximations until real capsule/mesh primitive colliders land.
        /// </summary>
        public static GameObject CreatePrimitive(CosmicShore.Engine.PrimitiveType type)
        {
            var go = new GameObject(type.ToString());
            var mesh = PrimitiveMeshes.GetShared(type);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            if (type == CosmicShore.Engine.PrimitiveType.Sphere)
                go.AddComponent<SphereCollider>().radius = 0.5f;
            else
                go.AddComponent<BoxCollider>().size = mesh.bounds.size;
            return go;
        }

        public override string name { get => base.name; set => base.name = value; }

        public bool activeInHierarchy => !destroyedFlag && ChainActive();

        // ── activeInHierarchy cache ───────────────────────────────────
        // The chain answer (every ancestor's activeSelf) is memoised per object against a
        // global epoch that any activeSelf or parent change bumps. It was a parent walk on
        // every call, and every isActiveAndEnabled goes through it: in a grown arena that is
        // tens of thousands of walks down deep flora hierarchies per frame. After a bump an
        // object recomputes through its parent's (memoised) answer, so siblings share one
        // walk. Semantics are unchanged: a cached value is only ever read at the epoch it
        // was computed at, and nothing the chain depends on can change without a bump.
        static long s_hierarchyEpoch;
        long _chainEpoch = -1;
        bool _chainActive;
        static readonly List<GameObject> s_chainScratch = new();

        /// <summary>Invalidate every cached activeInHierarchy answer (an activeSelf or parent changed).</summary>
        internal static void BumpHierarchyEpoch() => s_hierarchyEpoch++;

        bool ChainActive()
        {
            long epoch = s_hierarchyEpoch;
            if (_chainEpoch == epoch) return _chainActive;

            // Walk up to the first ancestor with a current answer (or the root), then fill
            // the answers back down. Iterative: flora and worm chains can be deep.
            var path = s_chainScratch;
            int baseCount = path.Count; // reentrancy-safe: never happens, but never corrupts
            bool above = true;
            for (GameObject go = this; go is not null; )
            {
                if (go._chainEpoch == epoch) { above = go._chainActive; break; }
                path.Add(go);
                var parent = go.transform?.parent;
                go = parent?.gameObject;
            }
            for (int i = path.Count - 1; i >= baseCount; i--)
            {
                var go = path[i];
                above = above && go.activeSelf;
                go._chainActive = above;
                go._chainEpoch = epoch;
            }
            path.RemoveRange(baseCount, path.Count - baseCount);
            return _chainActive;
        }

        /// <summary>Diagnostics: CS_PORT_TRACE_ACTIVE=&lt;exact GameObject name&gt; prints a stack for each activation change of that object.</summary>
        static readonly string s_traceActive = Environment.GetEnvironmentVariable("CS_PORT_TRACE_ACTIVE");

        public void SetActive(bool value)
        {
            if (activeSelf == value || destroyedFlag) return;
            if (s_traceActive != null && name == s_traceActive)
                Console.Error.WriteLine($"[trace-active] '{name}' SetActive({value}) frame {Time.frameCount}\n{Environment.StackTrace}");

            bool parentActive = transform.parent is null || transform.parent.gameObject.activeInHierarchy;
            activeSelf = value;
            BumpHierarchyEpoch();

            // Effective state only changes when every ancestor is active.
            if (parentActive) NotifyHierarchyActiveChanged(value);
        }

        /// <summary>
        /// Port surface: activate a freshly loaded scene's roots the way Unity's scene load does,
        /// in two phases. Phase 1 marks every root active (so the whole scene is active in
        /// hierarchy and findable by FindObjectsByType / FindAnyObjectByType) WITHOUT running
        /// any lifecycle; <paramref name="beforeLifecycle"/> runs at that point. Phase 2 then
        /// delivers Awake/OnEnable over each root. This is the slot Reflex's ContainerScope
        /// occupies in the original (its Awake, at execution order -1,000,000,000, injects the
        /// whole scene ahead of every other Awake/OnEnable, and its installers can still find
        /// the scene's not-yet-awake managers). Roots must be parentless and inactive.
        /// </summary>
        public static void ActivateSceneRoots(IReadOnlyList<GameObject> roots, Action beforeLifecycle)
        {
            var activated = new List<GameObject>(roots.Count);
            foreach (var root in roots)
            {
                if (root is null || root.destroyedFlag || root.activeSelf || root.transform.parent is not null) continue;
                root.activeSelf = true;
                activated.Add(root);
            }
            BumpHierarchyEpoch();

            beforeLifecycle?.Invoke();

            // Unity delivers a loaded scene's Awake/OnEnable in script execution order (each
            // behaviour's Awake then its OnEnable, scripts with a lower order first, load order
            // within one order). A scene with no ordered script keeps the plain hierarchy walk.
            var order = new List<(Component c, int index)>();
            foreach (var root in activated)
                if (!root.destroyedFlag && root.activeSelf) root.CollectActive(order);
            bool ordered = false;
            foreach (var (c, _) in order)
                if (c is MonoBehaviour m && m.ExecutionOrder != 0) { ordered = true; break; }
            if (!ordered)
            {
                foreach (var root in activated)
                    if (!root.destroyedFlag && root.activeSelf)
                        root.NotifyHierarchyActiveChanged(true);
                return;
            }
            // Colliders are in the physics scene from the moment the scene loads, before any Awake.
            foreach (var (c, _) in order)
                if (c is Collider collider && collider.enabled && !collider.gameObject.destroyedFlag && collider.gameObject.activeInHierarchy)
                    GameLoop.Current?.Triggers.NoteArrived(collider);
            var behaviours = new List<(MonoBehaviour mb, int index)>();
            foreach (var (c, i) in order) if (c is MonoBehaviour mb) behaviours.Add((mb, i));
            behaviours.Sort((a, b) => a.mb.ExecutionOrder != b.mb.ExecutionOrder ? a.mb.ExecutionOrder.CompareTo(b.mb.ExecutionOrder) : a.index.CompareTo(b.index));
            foreach (var (mb, _) in behaviours)
                // An earlier Awake may have destroyed or deactivated this one's object: then it does not wake.
                if (!mb.destroyedFlag && !mb.gameObject.destroyedFlag && mb.gameObject.activeInHierarchy)
                    mb.HandleHierarchyActive(true);
        }

        /// <summary>This active subtree's components in load (hierarchy) order, for an ordered scene activation.</summary>
        void CollectActive(List<(Component, int)> into)
        {
            foreach (var component in _components) into.Add((component, into.Count));
            foreach (var child in transform.Children)
                if (!child.gameObject.destroyedFlag && child.gameObject.activeSelf)
                    child.gameObject.CollectActive(into);
        }

        /// <summary>Propagate an effective-activation change to this subtree's behaviours.</summary>
        internal void NotifyHierarchyActiveChanged(bool active)
        {
            // Snapshot: callbacks may mutate the component list.
            var components = _components.ToArray();
            foreach (var component in components)
            {
                if (component is MonoBehaviour mb)
                    mb.HandleHierarchyActive(active);
                else if (active && component is Collider collider && collider.enabled)
                    GameLoop.Current?.Triggers.NoteArrived(collider); // enters the physics scene now, at its current pose
            }

            // Snapshot the children too — Awake/OnEnable in the recursion may
            // legally add or reparent siblings (original contract: hierarchy
            // mutation during activation callbacks is allowed; late additions
            // run their own activation when created).
            var children = System.Linq.Enumerable.ToArray(transform.Children);
            foreach (var child in children)
                if (!child.gameObject.destroyedFlag && child.gameObject.activeSelf)
                    child.gameObject.NotifyHierarchyActiveChanged(active);
        }

        // ── Components ───────────────────────────────────────────────

        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));

        [ThreadStatic] static int t_restoreDepth;

        /// <summary>
        /// When true, <see cref="AddComponent(Type)"/> honours [RequireComponent] the way a Unity
        /// player does. The player turns it on; the legacy hand-assembled test/sim harnesses
        /// (which attach every sibling themselves, in an order they choose) keep the default off,
        /// the same split as <c>NetworkManager.EmulateNetcodeLifecycle</c>.
        /// </summary>
        public static bool EnforceRequireComponent { get; set; }

        /// <summary>
        /// While open, <see cref="AddComponent(Type)"/> adds exactly what it is asked for — no
        /// [RequireComponent] dependencies. Used by paths that restore a serialized component
        /// graph verbatim (scene/prefab instantiation, Instantiate's clone), where every required
        /// component is already in the data and an auto-added one would be a duplicate.
        /// </summary>
        public static IDisposable ComponentGraphRestoreScope() => new RestoreScope();

        sealed class RestoreScope : IDisposable
        {
            bool _open = true;
            public RestoreScope() => t_restoreDepth++;
            public void Dispose() { if (_open) { _open = false; t_restoreDepth--; } }
        }

        /// <summary>Original contract: adding a component first adds whatever its [RequireComponent]s name.</summary>
        [ThreadStatic] static HashSet<Type> t_adding;

        void AddRequiredComponents(Type componentType)
        {
            t_adding ??= new HashSet<Type>();
            if (!t_adding.Add(componentType)) return; // mutual requirement: the outer add supplies it
            try
            {
            foreach (RequireComponentAttribute req in componentType.GetCustomAttributes(typeof(RequireComponentAttribute), inherit: true))
                foreach (var t in new[] { req.m_Type0, req.m_Type1, req.m_Type2 })
                {
                    if (t == null || t.IsAbstract || t.IsInterface || !typeof(Component).IsAssignableFrom(t)) continue;
                    if (typeof(Transform).IsAssignableFrom(t) ? t.IsInstanceOfType(transform) : GetComponent(t) != null) continue;
                    if (t_adding.Contains(t)) continue;
                    AddComponent(t);
                }
            }
            finally { t_adding.Remove(componentType); }
        }

        public Component AddComponent(Type componentType)
        {
            if (!typeof(Component).IsAssignableFrom(componentType))
                throw new ArgumentException($"{componentType.Name} is not a Component.");
            if (EnforceRequireComponent && t_restoreDepth == 0 && !typeof(Transform).IsAssignableFrom(componentType))
                AddRequiredComponents(componentType);

            // Transform-derived types (RectTransform) CONVERT the existing transform in
            // place rather than adding a second one (original contract: a GameObject has
            // exactly one transform; adding a RectTransform upgrades it, preserving the
            // hierarchy slot, children, and local pose). Adding a plain Transform is
            // meaningless — the object already has one.
            if (typeof(Transform).IsAssignableFrom(componentType))
            {
                if (componentType == typeof(Transform)) return transform;
                if (componentType.IsInstanceOfType(transform)) return transform; // already converted
                var replacement = (Transform)Activator.CreateInstance(componentType, nonPublic: true);
                replacement.gameObject = this;
                replacement.AdoptHierarchyFrom(transform);
                _components[_components.IndexOf(transform)] = replacement;
                transform = replacement;
                return replacement;
            }

            var component = (Component)Activator.CreateInstance(componentType, nonPublic: true);
            component.gameObject = this;
            SerializedFieldDefaults.Fill(component); // before Awake: the serializer's non-null guarantee
            _components.Add(component);
            if (component is Collider collider)
                GameLoop.Current.Triggers.Register(collider); // trigger-pass registry (creation order)
            if (component is Renderer renderer)
                Renderer.RegisterLive(renderer); // render backend registry
            if (component is Light light)
                LiveComponents<Light>.Register(light);
            if (component is Canvas canvas)
                LiveComponents<Canvas>.Register(canvas);
            if (component is Animator animator)
                LiveComponents<Animator>.Register(animator);
            if (component is Rigidbody rigidbody)
                GameLoop.Current.RegisterRigidbody(rigidbody); // E18 dynamics registry (creation order)
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
        static readonly string s_traceDestroy = System.Environment.GetEnvironmentVariable("CS_PORT_TRACE_DESTROY");

        internal void DestroyNow()
        {
            if (s_traceDestroy != null && name != null && name.Contains(s_traceDestroy, System.StringComparison.OrdinalIgnoreCase))
                System.Console.WriteLine($"[trace-destroy] '{name}' destroyed at:\n{System.Environment.StackTrace}");
            if (destroyedFlag) return;

            // Netcode contract: destroying a SPAWNED NetworkObject (a scene unload, a Destroy)
            // despawns it first, so every behaviour gets OnNetworkDespawn before any OnDestroy —
            // the in-scene spawners unsubscribe their SOAP handlers there.
            foreach (var component in _components)
                if (component is Networking.NetworkObject no && no.IsSpawned)
                {
                    // A client never despawns on the network; its copy of an object goes away
                    // locally when its scene unloads (the server despawns the authority copy).
                    if (Networking.NetDriver.IsClientOnly) no.DespawnRemote(destroy: false);
                    else no.Despawn(destroy: false);
                    break;
                }

            // Children first (snapshot — destruction mutates the list).
            var children = new List<Transform>(transform.Children);
            foreach (var child in children)
                child.gameObject.DestroyNow();

            // Components: behaviours get OnDisable/OnDestroy.
            var components = _components.ToArray();
            foreach (var component in components)
                component.DestroyComponentNow();
            _components.Clear();

            if (transform.parent is null) { scene?.RemoveRoot(this); transform.ReleaseWorldCacheForDestroy(); }
            else transform.SetParentForDestroy();

            destroyedFlag = true;
        }
    }
}
