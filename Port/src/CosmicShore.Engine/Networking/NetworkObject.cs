namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Original-contract NetworkObject component (E19 — the menu-swap/vessel-initializer arc;
    /// grew out of the E12 per-behaviour handle). In the original engine a NetworkObject is a
    /// component authored on every networked prefab root: spawn code looks it up
    /// (<c>prefab.TryGetComponent(out NetworkObject)</c>), instantiates it, and calls
    /// <see cref="SpawnWithOwnership"/>; teardown calls <see cref="Despawn"/>
    /// (<c>NetworkObject.Despawn(true)</c> in <c>Player.DestroyPlayer</c> /
    /// <c>VesselController.DestroyVessel</c> / <c>ServerPlayerVesselInitializer.DespawnVessel</c>).
    ///
    /// The port keeps per-behaviour replication ids (<see cref="NetworkBehaviour"/> allocates at
    /// Spawn), so this component fans spawn/despawn out to every <see cref="NetworkBehaviour"/>
    /// on its GameObject and children. A whole-object spawn allocates ONE id shared by every
    /// behaviour (the original contract); a behaviour spawned directly still reports through
    /// here (first spawned behaviour's id / owner).
    ///
    /// Ownership (live-code surface): <see cref="ChangeOwnership"/> updates every behaviour's
    /// owner record and raises <see cref="NetworkBehaviour.OnGainedOwnership"/> /
    /// <see cref="NetworkBehaviour.OnLostOwnership"/> locally — the single-process host is the
    /// only peer, so ownership changes are observed immediately and exactly once.
    ///
    /// A behaviour without an authored NetworkObject lazily gains one through
    /// <see cref="NetworkBehaviour.NetworkObject"/>, preserving the pre-component handle
    /// contract (same instance per GameObject, id proxy, Despawn(destroy)).
    /// </summary>
    public sealed class NetworkObject : MonoBehaviour
    {
        /// <summary>
        /// Original Netcode flag: despawn + destroy this object when its scene unloads.
        /// Recorded by <see cref="Spawn"/>/<see cref="SpawnWithOwnership"/>; data-only until the
        /// scene-management phase tracks scene membership (the single-scene engine unloads nothing).
        /// </summary>
        public bool DestroyWithScene { get; set; }

        /// <summary>Netcode flag: keep the object alive when its owner disconnects (data-only offline).</summary>
        public bool DontDestroyWithOwner { get; set; }

        /// <summary>Netcode flag: re-parenting is replicated (data-only offline).</summary>
        public bool AutoObjectParentSync { get; set; } = true;

        /// <summary>Netcode flag: object follows active-scene changes (data-only offline).</summary>
        public bool ActiveSceneSynchronization { get; set; }

        /// <summary>Netcode flag: scene migration is synchronized (data-only offline).</summary>
        public bool SceneMigrationSynchronization { get; set; } = true;

        /// <summary>Netcode flag: new clients observe the object on spawn (data-only offline).</summary>
        public bool SpawnWithObservers { get; set; } = true;

        /// <summary>Prefab identity hash (per-instance offline; not a wire hash).</summary>
        [SerializeField] internal uint GlobalObjectIdHash;

        NetworkBehaviour[] _spawnedBehaviours;

        /// <summary>The behaviours as captured at spawn: the order both peers address them by.</summary>
        internal NetworkBehaviour[] SpawnedBehaviours => _spawnedBehaviours ??= CollectBehaviours();

        internal NetworkBehaviour[] CollectBehaviours() => _spawnedBehaviours = Behaviours;

        /// <summary>
        /// True for objects that were placed in a scene rather than spawned from a prefab.
        /// Offline every networked object is spawned by code; authored data only (null = unknown).
        /// </summary>
        public bool? IsSceneObject { get; set; }

        /// <summary>True when this object is a client's player object (set by <see cref="SpawnAsPlayerObject"/>).</summary>
        public bool IsPlayerObject { get; private set; }

        /// <summary>The NetworkManager this object belongs to — the process singleton offline.</summary>
        public NetworkManager NetworkManager => NetworkManager.Singleton;

        bool _spawnedAsObject;
        ulong _objectId;
        ulong _ownerClientId;

        NetworkBehaviour[] Behaviours => gameObject.GetComponentsInChildren<NetworkBehaviour>(includeInactive: true);

        /// <summary>The behaviours on this object and its children (original: ChildNetworkBehaviours).</summary>
        public System.Collections.Generic.List<NetworkBehaviour> ChildNetworkBehaviours => new(Behaviours);

        /// <summary>
        /// Replication id of the object: the id allocated by a whole-object spawn, else the first
        /// SPAWNED behaviour's id in component order (falling back to the first behaviour's
        /// retained id, 0 when never spawned).
        /// </summary>
        public ulong NetworkObjectId
        {
            get
            {
                if (_spawnedAsObject && _objectId != 0) return _objectId;
                var behaviours = Behaviours;
                foreach (var behaviour in behaviours)
                    if (behaviour.IsSpawned)
                        return behaviour.NetworkObjectId;
                if (_objectId != 0) return _objectId;
                return behaviours.Length > 0 ? behaviours[0].NetworkObjectId : 0UL;
            }
        }

        /// <summary>True while the object (or any behaviour on it) is spawned.</summary>
        public bool IsSpawned
        {
            get
            {
                if (_spawnedAsObject) return true;
                foreach (var behaviour in Behaviours)
                    if (behaviour.IsSpawned)
                        return true;
                return false;
            }
        }

        /// <summary>Client id of the owner (the object's own record, else the first spawned behaviour's).</summary>
        public ulong OwnerClientId
        {
            get
            {
                if (_spawnedAsObject) return _ownerClientId;
                foreach (var behaviour in Behaviours)
                    if (behaviour.IsSpawned)
                        return behaviour.OwnerClientId;
                return _ownerClientId;
            }
        }

        /// <summary>True when the local client owns the object.</summary>
        public bool IsOwner => OwnerClientId == LocalClientId;

        /// <summary>True when the server owns the object.</summary>
        public bool IsOwnedByServer => OwnerClientId == NetworkManager.ServerClientId;

        /// <summary>True when this object is the local client's player object.</summary>
        public bool IsLocalPlayer => IsPlayerObject && IsOwner;

        /// <summary>Client-server topology: the server holds authority.</summary>
        public bool HasAuthority
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm == null || nm.IsServer;
            }
        }

        /// <summary>Spawn with local ownership (original signature).</summary>
        public void Spawn(bool destroyWithScene = false)
            => SpawnWithOwnership(LocalClientId, destroyWithScene);

        /// <summary>
        /// Spawn every NetworkBehaviour on this object (and children) with ownership assigned to
        /// <paramref name="ownerClientId"/>, all sharing ONE freshly-allocated object id — the
        /// original engine's contract (every behaviour's NetworkObjectId is its object's id), so
        /// id round-trips like <c>player.NetVesselId = vesselObject.NetworkObjectId</c> resolve
        /// regardless of component order. Role flags come from the active
        /// <see cref="NetworkManager.Singleton"/>; absent one, single-process host-mode defaults apply.
        /// </summary>
        public void SpawnWithOwnership(ulong ownerClientId, bool destroyWithScene = false)
        {
            DestroyWithScene = destroyWithScene;

            var nm = NetworkManager.Singleton;
            bool isServer = nm == null || nm.IsServer;
            bool isClient = nm == null || nm.IsClient;
            bool isOwner = ownerClientId == LocalClientId;
            ulong objectId = NetworkBehaviour.AllocateObjectId();

            _objectId = objectId;
            _ownerClientId = ownerClientId;
            _spawnedAsObject = true;
            if (GlobalObjectIdHash == 0) GlobalObjectIdHash = (uint)GetInstanceID();

            var behaviours = CollectBehaviours();
            foreach (var behaviour in behaviours) NetVarBinding.Bind(behaviour);
            if (nm != null)
                nm.SpawnManager?.Register(this);
            foreach (var behaviour in behaviours)
                if (!behaviour.IsSpawned)
                    behaviour.SpawnWithId(objectId, isServer, isClient, isOwner, ownerClientId);

            foreach (var behaviour in behaviours)
                if (behaviour.IsSpawned)
                    behaviour.RunPostSpawn();

            if (NetDriver.IsServer) NetDriver.OnServerSpawned(this);
        }

        /// <summary>Client: the server spawned this object (ids and owner come from the wire).</summary>
        internal void SpawnRemote(ulong objectId, ulong ownerClientId, bool isPlayerObject, bool destroyWithScene)
        {
            var nm = NetworkManager.Singleton;
            DestroyWithScene = destroyWithScene;
            IsPlayerObject = isPlayerObject;
            _objectId = objectId;
            _ownerClientId = ownerClientId;
            _spawnedAsObject = true;
            ulong local = LocalClientId;
            var behaviours = SpawnedBehaviours;
            foreach (var behaviour in behaviours) NetVarBinding.Bind(behaviour);
            nm?.SpawnManager?.Register(this);
            if (isPlayerObject && nm != null)
            {
                if (!nm.ConnectedClients.TryGetValue(ownerClientId, out var client))
                {
                    client = new NetworkClient { ClientId = ownerClientId };
                    nm.ConnectedClients[ownerClientId] = client;
                    nm.ConnectedClientsList.Add(client);
                }
                client.PlayerObject = this;
                if (ownerClientId == local) nm.LocalClient = client;
            }
            foreach (var behaviour in behaviours)
                if (!behaviour.IsSpawned)
                    behaviour.SpawnWithId(objectId, isServer: false, isClient: true, isOwner: ownerClientId == local, ownerClientId);
            foreach (var behaviour in behaviours)
                if (behaviour.IsSpawned)
                    behaviour.RunPostSpawn();
        }

        /// <summary>Client: the server despawned this object.</summary>
        internal void DespawnRemote(bool destroy)
        {
            NetworkManager.Singleton?.SpawnManager?.Unregister(this);
            foreach (var behaviour in SpawnedBehaviours)
                if (behaviour != null) behaviour.Despawn();
            _spawnedAsObject = false;
            if (destroy) Destroy(gameObject);
        }

        /// <summary>Client: the server changed the owner.</summary>
        internal void ApplyRemoteOwnership(ulong newOwner)
        {
            _ownerClientId = newOwner;
            ulong local = LocalClientId;
            foreach (var behaviour in SpawnedBehaviours)
                behaviour.ApplyOwnership(newOwner, local);
        }

        void OnDestroy()
        {
            if (!_spawnedAsObject) return;
            // Destroyed while spawned (scene unload, or Destroy without Despawn): despawn first.
            bool sceneUnload = DestroyWithScene;
            NetworkManager.Singleton?.SpawnManager?.Unregister(this);
            if (NetDriver.IsServer) NetDriver.OnServerDespawned(this, true, sceneUnload);
            foreach (var behaviour in SpawnedBehaviours)
                if (behaviour != null && behaviour.IsSpawned) behaviour.Despawn();
            _spawnedAsObject = false;
        }

        internal static readonly bool TraceNet = System.Environment.GetEnvironmentVariable("CS_PORT_TRACE_NET") == "1";

        /// <summary>Spawn as <paramref name="clientId"/>'s player object (records the client → player link).</summary>
        public void SpawnAsPlayerObject(ulong clientId, bool destroyWithScene = false)
        {
            IsPlayerObject = true;
            SpawnWithOwnership(clientId, destroyWithScene);
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var client))
            {
                client = new NetworkClient { ClientId = clientId };
                nm.ConnectedClients[clientId] = client;
                nm.ConnectedClientsList.Add(client);
            }
            client.PlayerObject = this;
            if (TraceNet) System.Console.WriteLine($"[trace-net] player object for client {clientId} = '{name}' (#{NetworkObjectId}) nm#{nm.GetInstanceID()}");
            if (clientId == nm.LocalClientId && nm.LocalClient == null)
                nm.LocalClient = client;
        }

        /// <summary>Instantiate <paramref name="prefab"/> and spawn it (original static helper).</summary>
        public static NetworkObject InstantiateAndSpawn(GameObject prefab, NetworkManager networkManager = null,
            ulong ownerClientId = 0, bool destroyWithScene = false, bool isPlayerObject = false,
            bool forceOverride = false, Vector3 position = default, Quaternion rotation = default)
        {
            var go = Instantiate(prefab, position, rotation);
            var no = go.GetComponent<NetworkObject>();
            if (no == null) no = go.AddComponent<NetworkObject>();
            if (isPlayerObject) no.SpawnAsPlayerObject(ownerClientId, destroyWithScene);
            else no.SpawnWithOwnership(ownerClientId, destroyWithScene);
            return no;
        }

        /// <summary>
        /// Transfer ownership to <paramref name="newOwnerClientId"/>: every behaviour's owner record
        /// updates and the gained/lost/changed ownership callbacks fire locally.
        /// </summary>
        public void ChangeOwnership(ulong newOwnerClientId)
        {
            if (NetDriver.IsClientOnly)
            {
                Debug.LogError("[Netcode] Only the server can change ownership.");
                return;
            }
            ulong local = LocalClientId;
            _ownerClientId = newOwnerClientId;
            foreach (var behaviour in Behaviours)
                behaviour.ApplyOwnership(newOwnerClientId, local);
            if (NetDriver.IsServer && IsSpawned) NetDriver.OnServerOwnershipChanged(this, newOwnerClientId);
        }

        /// <summary>Return ownership to the server.</summary>
        public void RemoveOwnership() => ChangeOwnership(NetworkManager.ServerClientId);

        /// <summary>Re-parent under another transform (single-process: a plain local re-parent).</summary>
        public bool TrySetParent(Transform parent, bool worldPositionStays = true)
        {
            if (NetDriver.IsClientOnly && IsSpawned) return false; // only the server re-parents a spawned object
            transform.SetParent(parent, worldPositionStays);
            if (NetDriver.IsServer && IsSpawned)
                NetDriver.OnServerParentChanged(this, parent != null ? parent.GetComponentInParent<NetworkObject>() : null, worldPositionStays);
            foreach (var behaviour in SpawnedBehaviours)
                if (behaviour != null && behaviour.IsSpawned) behaviour.OnNetworkObjectParentChanged(parent != null ? parent.GetComponentInParent<NetworkObject>() : null);
            return true;
        }

        public bool TrySetParent(GameObject parent, bool worldPositionStays = true)
            => TrySetParent(parent != null ? parent.transform : null, worldPositionStays);

        public bool TrySetParent(NetworkObject parent, bool worldPositionStays = true)
            => TrySetParent(parent != null ? parent.transform : null, worldPositionStays);

        public bool TryRemoveParent(bool worldPositionStays = true) => TrySetParent((Transform)null, worldPositionStays);

        /// <summary>Visibility is universal in a single-process session.</summary>
        public bool IsNetworkVisibleTo(ulong clientId) => IsSpawned;

        public void NetworkShow(ulong clientId) { }
        public void NetworkHide(ulong clientId) { }

        public void Despawn(bool destroy = true)
        {
            if (NetDriver.IsClientOnly && _spawnedAsObject)
            {
                Debug.LogError($"[Netcode] Only the server can despawn '{name}'.");
                return;
            }
            var nm = NetworkManager.Singleton;
            if (nm != null)
                nm.SpawnManager?.Unregister(this);
            if (_spawnedAsObject && NetDriver.IsServer) NetDriver.OnServerDespawned(this, destroy, bySceneUnload: false);

            foreach (var behaviour in Behaviours)
                behaviour.Despawn();

            _spawnedAsObject = false;

            if (destroy)
                Destroy(gameObject);
        }

        static ulong LocalClientId
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm == null ? 0UL : nm.LocalClientId;
            }
        }
    }
}
