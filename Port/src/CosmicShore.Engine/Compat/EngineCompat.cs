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

    /// <summary>
    /// Universal-RPC target set (Netcode 2.x `[Rpc(SendTo.…)]` — engine addition for the
    /// Tournament arc: <c>TournamentLobbyNetwork.SetReadyRpc</c>). Metadata only until the
    /// transport phase; RPCs local-invoke (Player/RoundStats precedent).
    /// </summary>
    public enum SendTo
    {
        Everyone = 0,
        Owner = 1,
        NotOwner = 2,
        Server = 3,
        NotServer = 4,
        ClientsAndHost = 5,
        Me = 6,
        NotMe = 7,
        SpecifiedInParams = 8,
        Authority = 9,
        NotAuthority = 10,
    }

    /// <summary>Marks a universal RPC (Netcode 2.x). Local-invoke semantics until the transport phase.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RpcAttribute : Attribute
    {
        public SendTo Target { get; }
        public bool RequireOwnership;
        public RpcAttribute(SendTo target) => Target = target;
    }

    /// <summary>Receive-side metadata for a universal RPC invocation.</summary>
    public struct RpcReceiveParams
    {
        /// <summary>ClientId of the sender. Local-invoke (single-process) semantics: 0 — the host.</summary>
        public ulong SenderClientId;
    }

    /// <summary>Optional last parameter of a universal RPC (original contract: `RpcParams rpcParams = default`).</summary>
    public struct RpcParams
    {
        public RpcReceiveParams Receive;
    }
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
        public static int sleepTimeout = SleepTimeout.SystemSetting;
        public static bool autorotateToPortrait;
        public static bool autorotateToPortraitUpsideDown;
        public static bool autorotateToLandscapeLeft;
        public static bool autorotateToLandscapeRight;
        public static Resolution currentResolution => new() { width = width, height = height, refreshRate = 60 };
    }

    /// <summary>Original contract: UnityEngine.SleepTimeout constants.</summary>
    public static class SleepTimeout
    {
        public const int NeverSleep = -1;
        public const int SystemSetting = -2;
    }

    /// <summary>Original contract: UnityEngine.QualitySettings (the slice ported bootstrap config drives).</summary>
    public static class QualitySettings
    {
        public static int vSyncCount;
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

        /// <summary>
        /// Original-contract URL open (e.g. the painting toy's web-share viewer). Headless has no
        /// browser; log the intent so the flow is observable. A future platform layer may launch it.
        /// </summary>
        public static void OpenURL(string url) => Debug.Log($"[Application] OpenURL: {url}");

        /// <summary>
        /// Original-contract reachability read (party-system arc: NetworkDiagnostics
        /// snapshots it). Headless default assumes a LAN; a harness or the future
        /// platform layer may reassign.
        /// </summary>
        public static NetworkReachability internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork;
    }

    /// <summary>Original UnityEngine.NetworkReachability values (frozen).</summary>
    public enum NetworkReachability
    {
        NotReachable = 0,
        ReachableViaCarrierDataNetwork = 1,
        ReachableViaLocalAreaNetwork = 2,
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

        /// <summary>
        /// Layer-masked non-alloc variant (original-engine contract): a collider
        /// qualifies when the bit for its GameObject's layer is set in the mask.
        /// </summary>
        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask)
            => GameLoop.Current?.Triggers.OverlapSphereNonAlloc(position, radius, results, layerMask) ?? 0;

        /// <summary>All layers — the original engine's default mask for overlap queries.</summary>
        public const int AllLayers = ~0;

        /// <summary>
        /// Allocating sphere query (original-engine contract): every registered, active
        /// collider overlapping the sphere whose GameObject layer is in
        /// <paramref name="layerMask"/>, in deterministic registration order.
        /// </summary>
        public static Collider[] OverlapSphere(Vector3 position, float radius, int layerMask = AllLayers)
            => GameLoop.Current?.Triggers.OverlapSphere(position, radius, layerMask) ?? Array.Empty<Collider>();

        /// <summary>
        /// Box occupancy query (original-engine contract): true when any registered,
        /// active collider overlaps the box.
        /// </summary>
        public static bool CheckBox(Vector3 center, Vector3 halfExtents)
            => GameLoop.Current?.Triggers.CheckBox(center, halfExtents) ?? false;

        /// <summary>
        /// Oriented overload. The trigger pass treats boxes as world-space AABBs
        /// (rotation ignored — same phase-2 deviation as <see cref="TriggerPass"/> box
        /// overlap); the orientation parameter is accepted for source compatibility and
        /// gains effect with the full physics phase.
        /// </summary>
        public static bool CheckBox(Vector3 center, Vector3 halfExtents, Quaternion orientation)
            => CheckBox(center, halfExtents);
    }

    /// <summary>Original-engine force application modes (UnityEngine.ForceMode). Values frozen.</summary>
    public enum ForceMode { Force = 0, Impulse = 1, VelocityChange = 2, Acceleration = 5 }

    /// <summary>Original-engine rigidbody interpolation modes. Data-only headless (no render frames to interpolate between).</summary>
    public enum RigidbodyInterpolation { None = 0, Interpolate = 1, Extrapolate = 2 }

    /// <summary>Original-engine collision detection modes. Data-only headless (the trigger pass is exact per fixed step).</summary>
    public enum CollisionDetectionMode { Discrete = 0, Continuous = 1, ContinuousDynamic = 2, ContinuousSpeculative = 3 }

    /// <summary>Original-engine physic-material combine modes. Values frozen.</summary>
    public enum PhysicsMaterialCombine { Average = 0, Multiply = 1, Minimum = 2, Maximum = 3 }

    /// <summary>
    /// Original-engine PhysicsMaterial (contact restitution/friction data). Data-only:
    /// the headless engine resolves no contact pairs, so combine modes and friction are
    /// carried for the authored setup code (e.g. AstroLeagueBall.Awake) to port verbatim.
    /// </summary>
    public class PhysicsMaterial : Object
    {
        public float bounciness;
        public float dynamicFriction = 0.6f;
        public float staticFriction = 0.6f;
        public PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Average;
        public PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Average;

        public PhysicsMaterial() { }
        public PhysicsMaterial(string name) { this.name = name; }
    }

    /// <summary>
    /// Rigidbody with minimal ballistic dynamics (E18 — the Astro League ball arc).
    /// Non-kinematic bodies integrate once per fixed step, AFTER the FixedUpdate phase
    /// (matching the original engine's "callbacks, then simulation" order inside the
    /// physics step — see <see cref="GameLoop.RunFixedSteps"/>):
    ///
    ///   • linear:  damping (PhysX-style <c>v *= max(0, 1 − damping·dt)</c>), then
    ///     <c>transform.position += linearVelocity · dt</c>. No gravity is simulated —
    ///     the HyperSea has none (every ported dynamic body sets useGravity = false);
    ///     the flag is carried as data only.
    ///   • angular: damping, clamp to <see cref="maxAngularVelocity"/>, then rotate about
    ///     the world-space angular-velocity axis (radians/sec, original convention).
    ///
    /// Contact RESOLUTION stays out of scope: the headless engine has no solver, so
    /// OnCollisionEnter/Stay never fire (ported code's trigger paths carry vessel
    /// contacts — see TriggerPass) and interpenetration is prevented by gameplay-side
    /// depenetration (e.g. AstroLeagueBall.EjectBallFromVessel), never by the engine.
    /// AddTorque uses a unit inertia tensor (the original engine's no-collider default);
    /// spin magnitudes are gameplay-cosmetic and clamped by <see cref="maxAngularVelocity"/>.
    /// Kinematic bodies keep the old placeholder behavior (pure data).
    /// </summary>
    public class Rigidbody : Component
    {
        public bool isKinematic;
        public bool useGravity;
        public float mass = 1f;
        public Vector3 velocity;

        /// <summary>Linear velocity in world units/sec (the original's linearVelocity alias of velocity).</summary>
        public Vector3 linearVelocity
        {
            get => velocity;
            set => velocity = value;
        }

        /// <summary>Angular velocity in radians/sec about world axes.</summary>
        public Vector3 angularVelocity;

        public float linearDamping;
        public float angularDamping = 0.05f;
        public float maxAngularVelocity = 7f;

        public RigidbodyInterpolation interpolation = RigidbodyInterpolation.None;
        public CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.Discrete;

        /// <summary>Local-space center of mass (default: the transform origin).</summary>
        public Vector3 centerOfMass;

        public Vector3 worldCenterOfMass => transform.TransformPoint(centerOfMass);

        /// <summary>
        /// Physics-authoritative position. The headless engine has no deferred physics
        /// transform buffer, so reads/writes go straight to the transform (documented
        /// deviation from the original's end-of-step sync — observable order is identical
        /// at the fixed-step granularity ported code samples at).
        /// </summary>
        public Vector3 position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public Quaternion rotation
        {
            get => transform.rotation;
            set => transform.rotation = value;
        }

        public void AddTorque(Vector3 torque, ForceMode mode = ForceMode.Force)
        {
            // Unit inertia tensor (the original engine's no-collider default, I = (1,1,1)):
            // Δω = τ for the instantaneous modes, τ·dt for the continuous ones.
            angularVelocity += mode switch
            {
                ForceMode.Impulse or ForceMode.VelocityChange => torque,
                _ => torque * Time.fixedDeltaTime,
            };
            ClampAngular();
        }

        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            velocity += mode switch
            {
                ForceMode.Impulse => force / Mathf.Max(0.0001f, mass),
                ForceMode.VelocityChange => force,
                ForceMode.Acceleration => force * Time.fixedDeltaTime,
                _ => force * Time.fixedDeltaTime / Mathf.Max(0.0001f, mass),
            };
        }

        internal void Integrate(float dt)
        {
            if (isKinematic) return;

            if (linearDamping > 0f) velocity *= Mathf.Max(0f, 1f - linearDamping * dt);
            if (angularDamping > 0f) angularVelocity *= Mathf.Max(0f, 1f - angularDamping * dt);
            ClampAngular();

            if (velocity.sqrMagnitude > 0f)
                transform.position += velocity * dt;

            float w = angularVelocity.magnitude;
            if (w > 1e-6f)
                transform.rotation = Quaternion.AngleAxis(w * Mathf.Rad2Deg * dt, angularVelocity / w) * transform.rotation;
        }

        void ClampAngular()
        {
            float w = angularVelocity.magnitude;
            if (w > maxAngularVelocity && w > 0f)
                angularVelocity *= maxAngularVelocity / w;
        }

        internal override void DestroyComponentNow()
        {
            GameLoop.Current?.UnregisterRigidbody(this);
            base.DestroyComponentNow();
        }
    }

    /// <summary>
    /// Data-only stand-in for UnityEngine.CanvasGroup (the UI-fade arc):
    /// <see cref="alpha"/> / <see cref="interactable"/> / <see cref="blocksRaycasts"/> carry the
    /// original contract so fade logic (MenuCrystalClickHandler, TournamentSceneView) runs
    /// verbatim headless; the render backend gives alpha a visual meaning later.
    /// </summary>
    public class CanvasGroup : Behaviour
    {
        public float alpha = 1f;
        public bool interactable = true;
        public bool blocksRaycasts = true;
        public bool ignoreParentGroups;
    }

    public class Collider : Behaviour
    {
        public bool isTrigger;

        /// <summary>Per-collider contact material (data-only headless — see <see cref="PhysicsMaterial"/>).</summary>
        public PhysicsMaterial material;

        /// <summary>
        /// Layers this collider never collides with. Data-only in the headless engine:
        /// the trigger pass ignores it (the ported call sites — e.g. the Astro League
        /// ball excluding TrailBlocks — pair a NON-trigger collider with other
        /// non-triggers, which the pass already skips entirely). Gains effect with the
        /// full physics phase.
        /// </summary>
        public LayerMask excludeLayers;

        public virtual Vector3 ClosestPoint(Vector3 position) => transform.position;

        /// <summary>
        /// World-space AABB of this collider (original contract). Phase-2 convention —
        /// rotation ignored: center transformed through the hierarchy, extents scaled by
        /// |lossyScale| (the same AABB the <see cref="TriggerPass"/> overlaps with). The
        /// base collider reports a degenerate point at the transform position.
        /// </summary>
        public virtual Bounds bounds => new Bounds(transform.position, Vector3.zero);

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

        public override Bounds bounds
        {
            get
            {
                Vector3 s = transform.lossyScale;
                var worldSize = new Vector3(
                    Mathf.Abs(size.x * s.x),
                    Mathf.Abs(size.y * s.y),
                    Mathf.Abs(size.z * s.z));
                return new Bounds(transform.TransformPoint(center), worldSize);
            }
        }
    }

    public class SphereCollider : Collider
    {
        public Vector3 center = Vector3.zero;
        public float radius = 0.5f;

        public override Bounds bounds
        {
            get
            {
                Vector3 s = transform.lossyScale;
                float r = radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                return new Bounds(transform.TransformPoint(center), new Vector3(r * 2f, r * 2f, r * 2f));
            }
        }
    }

    /// <summary>
    /// Mesh-shaped collider (the mesh arc). Original contract: <see cref="sharedMesh"/> is
    /// the collision mesh, <see cref="convex"/> requests a convex hull (required for
    /// Rigidbody interaction in the original engine — data-only here).
    ///
    /// Overlap semantics headless: the TriggerPass treats a MeshCollider as its mesh's
    /// LOCAL-BOUNDS AABB (bounds center transformed through the hierarchy, extents scaled
    /// by |lossyScale|, rotation ignored — the same phase-2 convention box colliders use).
    /// A null/destroyed mesh never overlaps anything. True per-triangle / convex-hull
    /// collision arrives with the full physics phase.
    /// </summary>
    public class MeshCollider : Collider
    {
        public Mesh sharedMesh;
        public bool convex;

        public override Bounds bounds
        {
            get
            {
                var shared = sharedMesh;
                if (shared is null || shared.IsDestroyed) return new Bounds(transform.position, Vector3.zero);
                Bounds local = shared.bounds;
                Vector3 s = transform.lossyScale;
                var worldSize = new Vector3(
                    Mathf.Abs(local.extents.x * s.x) * 2f,
                    Mathf.Abs(local.extents.y * s.y) * 2f,
                    Mathf.Abs(local.extents.z * s.z) * 2f);
                return new Bounds(transform.TransformPoint(local.center), worldSize);
            }
        }
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
            var clone = InstantiateDeferred(original, out var pendingActivation);
            pendingActivation?.SetActive(true);
            return clone;
        }

        /// <summary>
        /// Original-contract clone with DEFERRED activation. The clone tree is built fully
        /// deactivated so no Awake/OnEnable can observe a half-copied object — the original
        /// engine initializes the ENTIRE clone (hierarchy + serialized data) before any
        /// lifecycle hook runs, whereas the previous per-component AddComponent path fired
        /// hooks before <see cref="CopyFields"/>, so an ACTIVE template's clone saw default
        /// fields in Awake/OnEnable. When the source root was activeSelf,
        /// <paramref name="pendingActivation"/> returns the still-inactive clone root — the
        /// caller activates it AFTER applying any pose/parent arguments, matching the
        /// original Instantiate(position, rotation) contract where Awake already sees the
        /// final placement. Null when the source was inactive (nothing to activate).
        /// </summary>
        internal static T InstantiateDeferred<T>(T original, out GameObject pendingActivation) where T : Object
        {
            pendingActivation = null;
            switch (original)
            {
                case ScriptableObject so:
                {
                    var clone = (ScriptableObject)CloneViaMemberwise(so);
                    clone.name = so.name + "(Clone)";
                    return clone as T;
                }
                case GameObject go:
                {
                    var clone = CloneGameObject(go);
                    if (go.activeSelf) pendingActivation = clone;
                    return clone as T;
                }
                case Component component:
                {
                    var cloned = CloneGameObject(component.gameObject);
                    if (component.gameObject.activeSelf) pendingActivation = cloned;
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
            // EVERY node is built deactivated (original contract: no lifecycle hook may
            // run until the whole clone — components, children, serialized fields — is in
            // place; a child built as a free-standing active root would Awake at
            // AddComponent time and then have its cached state clobbered by CopyFields).
            // Children restore their authored activeSelf under their (still-inactive)
            // parent below; the ROOT stays inactive — InstantiateDeferred reports an
            // originally-active root back to the caller for activation after pose/parent
            // placement, so Awake sees the final placement like the original engine.
            var clone = new GameObject(isRoot ? source.name + "(Clone)" : source.name);
            clone.SetActive(false);

            // A RectTransform source clones as a RectTransform WITH its rect data
            // (original contract — UI prefabs keep their authored layout). Without
            // this the clone came up as a plain Transform and the first Graphic
            // access converted it lazily MID-ACTIVATION, mutating the parent's
            // child list under the activation walk and dropping every anchor.
            if (source.transform is RectTransform sourceRect)
            {
                var rect = clone.AddComponent<RectTransform>(); // in-place conversion
                rect.anchorMin = sourceRect.anchorMin;
                rect.anchorMax = sourceRect.anchorMax;
                rect.pivot = sourceRect.pivot;
                rect.anchoredPosition = sourceRect.anchoredPosition;
                rect.sizeDelta = sourceRect.sizeDelta;
            }

            map[source] = clone;
            map[source.transform] = clone.transform;

            clone.layer = source.layer;
            clone.tag = source.tag;
            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localRotation = source.transform.localRotation;
            clone.transform.localScale = source.transform.localScale;

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
                // Restore authored activeSelf AFTER parenting: the parent chain contains
                // this (inactive) node, so no hooks can fire yet — the subtree goes live
                // in one pass when the root is finally activated.
                if (child.gameObject.activeSelf) childClone.SetActive(true);
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
        ///   • rank-1 array / <see cref="List{T}"/> / <see cref="Dictionary{TKey,TValue}"/>
        ///     → NEW container, elements rerun through these same rules (the original
        ///     engine never shares a mutable container instance between a template and its
        ///     clone, whatever the element type — for non-serialized runtime dictionaries
        ///     the original keeps each instance's own field-initializer state, so a shared
        ///     reference would leak one clone's runtime state into every sibling: the
        ///     rung-3 ResourceSystem.ElementalLevels cross-pilot contamination);
        ///   • plain [Serializable] class → independent deep clone per field path. The
        ///     original engine inlines plain classes BY VALUE, so aliasing within the
        ///     template's plain-object graph intentionally does not survive cloning; graphs
        ///     truncate to null past <see cref="MaxPlainObjectDepth"/>;
        ///   • everything else (delegate fields on components, framework types,
        ///     multidimensional arrays) → reference copy — the port's existing
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

                case IDictionary dictionary when value.GetType() is { IsGenericType: true } dictionaryType
                                                 && dictionaryType.GetGenericTypeDefinition() == typeof(Dictionary<,>):
                    return CloneDictionaryValue(dictionary, dictionaryType, map, depth);

                // HashSet<T> — same container rule as Array/List/Dictionary: the original
                // engine never shares a mutable container instance between a template and
                // its clone. A shared runtime set leaks one clone's state into every
                // sibling (found via BranchingFlora.activeBranches: every flora clone grew
                // — and dropped its guaranteed initial leaf — on ONE shared trunk set, so
                // two of three flora were born leafless and failsafe-died).
                case IEnumerable when value.GetType() is { IsGenericType: true } setType
                                      && setType.GetGenericTypeDefinition() == typeof(HashSet<>):
                    return CloneHashSetValue((IEnumerable)value, setType, map, depth);

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

        static IDictionary CloneDictionaryValue(IDictionary dictionary, Type dictionaryType,
            Dictionary<object, object> map, int depth)
        {
            // Fresh container carrying the source's comparer; keys and values rerun
            // through the same value rules as array/list elements.
            object comparer = dictionaryType.GetProperty("Comparer")!.GetValue(dictionary);
            var copy = (IDictionary)Activator.CreateInstance(
                dictionaryType, new object[] { dictionary.Count, comparer });
            foreach (DictionaryEntry entry in dictionary)
            {
                object key = RemapValue(entry.Key, map, depth);
                object value = entry.Value is null ? null : RemapValue(entry.Value, map, depth);
                copy.Add(key, value);
            }
            return copy;
        }

        static object CloneHashSetValue(IEnumerable set, Type setType, Dictionary<object, object> map, int depth)
        {
            // Fresh container carrying the source's comparer; elements rerun through the
            // same value rules as array/list elements (value-type elements are kept as-is,
            // matching the List<T> path's shallow value copy).
            Type elementType = setType.GetGenericArguments()[0];
            object comparer = setType.GetProperty("Comparer")!.GetValue(set);
            object copy = Activator.CreateInstance(setType, new[] { comparer });
            MethodInfo add = setType.GetMethod("Add")!;

            bool remapElements = !elementType.IsValueType && elementType != typeof(string);
            foreach (var element in set)
            {
                object item = element is not null && remapElements
                    ? RemapValue(element, map, depth)
                    : element;
                add.Invoke(copy, new[] { item });
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
