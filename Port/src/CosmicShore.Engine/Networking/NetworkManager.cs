using CosmicShore.Engine.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Minimal stand-in for Unity Netcode's <c>NetworkManager</c> singleton (engine
    /// addition for V10: GameDataSO consults it). Ported code only checks
    /// <see cref="Singleton"/> for null/fake-null ("no networking active") plus the
    /// role flags; the session/transport phase replaces this with a real network
    /// driver. A null <see cref="Singleton"/> models the offline / single-process
    /// case, which is the default until something assigns one. Defaults model
    /// single-process host-mode, matching <see cref="NetworkBehaviour.Spawn"/>.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Singleton { get; set; }

        /// <summary>
        /// When true (the player turns it on), instances follow Netcode's own lifecycle: a
        /// NetworkManager starts idle (not listening, no clients), registers itself as
        /// <see cref="Singleton"/> on enable and survives scene loads, and <see cref="StartHost"/>
        /// runs connection approval and spawns the local player object. Off (the default) keeps
        /// the always-listening single-process stand-in the headless test harness is built on.
        /// </summary>
        public static bool EmulateNetcodeLifecycle { get; set; }

        /// <summary>The prefab's serialized "NetworkConfig:" block; adopted as <see cref="NetworkConfig"/> on Awake.</summary>
        [SerializeField, CosmicShore.Engine.Serialization.FormerlySerializedAs("NetworkConfig")] internal NetworkConfig NetworkConfigSerialized;
        public bool RunInBackground = true;
        public bool DontDestroy = true;

        void Awake()
        {
            if (!EmulateNetcodeLifecycle) return;
            if (NetworkConfigSerialized != null) NetworkConfig = NetworkConfigSerialized;
            IsServer = false;
            IsClient = false;
            IsListening = false;
            ConnectedClientsIds.Clear();
        }

        void OnEnable()
        {
            if (!EmulateNetcodeLifecycle) return;
            if (DontDestroy) DontDestroyOnLoad(gameObject);
            if (Singleton == null) SetSingleton();
        }

        void OnDestroy()
        {
            if (!EmulateNetcodeLifecycle) return;
            if (IsListening) Shutdown();
            if (ReferenceEquals(Singleton, this)) Singleton = null;
        }

        public bool IsServer { get; set; } = true;
        public bool IsClient { get; set; } = true;
        public bool IsHost => IsServer && IsClient;
        public bool IsListening { get; set; } = true;

        /// <summary>
        /// This machine's client id (engine addition for the vessel-initializer arc:
        /// <c>ServerPlayerVesselInitializer.NotifyClients</c> excludes the host by
        /// <c>LocalClientId</c>, and <see cref="NetworkObject.SpawnWithOwnership"/> derives
        /// the owner flag from it). 0 — the host — in single-process host-mode.
        /// </summary>
        public ulong LocalClientId { get; set; }

        /// <summary>
        /// Server-synchronized clock (engine addition for SA1: TimePlayedScoring reads
        /// <c>ServerTime.Time</c> when a NetworkManager is listening). Assigned by the
        /// harness / future network driver; defaults to an unstarted clock (0).
        /// </summary>
        public NetworkTime ServerTime { get; set; }

        /// <summary>
        /// Connected client ids (engine addition for the controller-chain arc:
        /// MultiplayerDomainGamesController counts humans by <c>ConnectedClientsIds.Count</c>
        /// and solo-session gates check <c>Count &lt;= 1</c>). Defaults to the single host
        /// client (id 0) — the single-process host-mode the rest of this stand-in models.
        /// The session/transport phase maintains it from real connections.
        /// </summary>
        public System.Collections.Generic.List<ulong> ConnectedClientsIds { get; } = new() { 0 };

        /// <summary>
        /// Server-side client table (engine addition for the vessel-initializer arc:
        /// <c>ProcessPreExistingPlayers</c> / <c>FindUnprocessedPlayerByOwnerClientId</c>
        /// discover persistent Players through <c>ConnectedClients[id].PlayerObject</c>).
        /// Empty by default — single-process host-mode discovers its players through
        /// <c>GameDataSO.Players</c>; the session/transport phase maintains it from real
        /// connections.
        /// </summary>
        public System.Collections.Generic.Dictionary<ulong, NetworkClient> ConnectedClients { get; } = new();

        /// <summary>
        /// List view of the connected clients (original surface iterated by
        /// <c>ServerPlayerVesselInitializer.NotifyClients</c> and
        /// <c>MenuServerPlayerVesselInitializer.NotifyClientsOfSwap</c>). Empty by default —
        /// no remote clients exist until the transport phase.
        /// </summary>
        public System.Collections.Generic.List<NetworkClient> ConnectedClientsList { get; } = new();

        /// <summary>
        /// Spawned-object registry (original surface read by
        /// <c>ClientPlayerVesselInitializer.ReRegisterPersistentPlayers</c>). Populated by
        /// <see cref="NetworkObject.SpawnWithOwnership"/> while a Singleton is assigned.
        /// </summary>
        public NetworkSpawnManager SpawnManager { get; set; } = new();

        /// <summary>
        /// Placeholder for Netcode's server-authoritative scene manager
        /// (<c>NetworkManager.SceneManager</c>). In the single-process port a "network"
        /// scene load IS a local load: <see cref="NetworkSceneManager.LoadScene"/> delegates
        /// to the engine <c>SceneManagement.SceneManager.LoadSceneAsync</c> (fire-and-forget,
        /// per Netcode's void contract) so loaders that poll the active scene name observe
        /// the same convergence a replicated load would produce. Client-side replication
        /// arrives with the transport phase.
        /// </summary>
        public NetworkSceneManager SceneManager { get; set; } = new();

        /// <summary>
        /// The local machine's connection record (original surface —
        /// <c>MainMenuController.ApplyMenuVesselClassToHost</c> reaches the host Player
        /// through <c>LocalClient.PlayerObject</c>). Null by default — single-process
        /// host-mode harnesses assign it when they spawn the persistent Player.
        /// </summary>
        public NetworkClient LocalClient { get; set; }

        // ── Netcode callback surface (engine addition for the transport arc:
        // MultiplayerSetup wires these in EnsureHostStarted; a future transport driver
        // raises the events for real remote connections) ─────────────────────────────

        /// <summary>
        /// Original-contract connection-approval hook (a public delegate FIELD in Netcode,
        /// not an event — call sites use += / -=). When set, <see cref="StartHost"/> runs the
        /// local (host) client through it, honoring <see cref="ConnectionApprovalResponse.Approved"/>;
        /// the transport phase runs every remote connection through it.
        /// </summary>
        public System.Action<ConnectionApprovalRequest, ConnectionApprovalResponse> ConnectionApprovalCallback;

        /// <summary>Raised per disconnected client id. Single-process host-mode never raises it —
        /// the transport phase does (hard drops beat the graceful UGS PlayerLeaving).</summary>
        public event System.Action<ulong> OnClientDisconnectCallback;

        /// <summary>Raised when the underlying transport fails. Single-process host-mode never
        /// raises it — the transport phase does.</summary>
        public event System.Action OnTransportFailure;

        /// <summary>Transport-driver entry points for the events above (the engine's stand-in for
        /// Netcode's internal raise paths; harnesses/tests may call them directly).</summary>
        public void NotifyClientDisconnect(ulong clientId)
        {
            OnClientDisconnectCallback?.Invoke(clientId);
            OnConnectionEvent?.Invoke(this, new ConnectionEventData { ClientId = clientId, EventType = ConnectionEvent.ClientDisconnected });
        }

        public void NotifyTransportFailure() => OnTransportFailure?.Invoke();

        /// <summary>Transport-driver entry point: a client finished connecting (raises the connected callbacks).</summary>
        public void NotifyClientConnected(ulong clientId)
        {
            if (!ConnectedClientsIds.Contains(clientId)) ConnectedClientsIds.Add(clientId);
            OnClientConnectedCallback?.Invoke(clientId);
            OnConnectionEvent?.Invoke(this, new ConnectionEventData { ClientId = clientId, EventType = ConnectionEvent.ClientConnected });
        }

        // ── Netcode 2.x surface for the live game code (offline single-process host) ──

        /// <summary>The server's client id — always 0.</summary>
        public const ulong ServerClientId = 0;

        /// <summary>Configuration record (player prefab, transport, approval payload, tick rate).</summary>
        public NetworkConfig NetworkConfig { get; set; } = new();


        /// <summary>Raised after the server side starts (StartHost/StartServer).</summary>
        public event System.Action OnServerStarted;

        /// <summary>Raised after the client side starts (StartHost/StartClient).</summary>
        public event System.Action OnClientStarted;

        /// <summary>Raised when the server side stops; the argument is "was a host".</summary>
        public event System.Action<bool> OnServerStopped;

        /// <summary>Raised when the client side stops; the argument is "was a host".</summary>
        public event System.Action<bool> OnClientStopped;

        /// <summary>Raised per connected client id (the host's own id on StartHost).</summary>
        public event System.Action<ulong> OnClientConnectedCallback;

        /// <summary>Unified connection-event stream (Netcode 2.x).</summary>
        public event System.Action<NetworkManager, ConnectionEventData> OnConnectionEvent;

        /// <summary>Raised before Shutdown tears the session down.</summary>
        public event System.Action OnPreShutdown;

        /// <summary>True between a successful start of the client side and shutdown.</summary>
        public bool IsConnectedClient => IsListening && IsClient;

        /// <summary>Shutdown is synchronous offline — never observed in progress.</summary>
        public bool ShutdownInProgress { get; private set; }

        /// <summary>Last disconnect reason (none offline).</summary>
        public string DisconnectReason { get; set; } = string.Empty;

        /// <summary>Tick system record (rate mirrors <see cref="NetworkConfig.TickRate"/>).</summary>
        public NetworkTickSystem NetworkTickSystem => _tickSystem ??= new NetworkTickSystem(this);
        NetworkTickSystem _tickSystem;

        /// <summary>Local clock — the same unsynchronized clock as <see cref="ServerTime"/> offline.</summary>
        public NetworkTime LocalTime => ServerTime;

        /// <summary>True when this manager is running in distributed-authority mode (never offline).</summary>
        public bool DistributedAuthorityMode => false;

        /// <summary>Prefab handler registry (instantiation overrides are data-only offline).</summary>
        public NetworkPrefabHandler PrefabHandler { get; } = new();

        /// <summary>Custom-message manager (loopback offline).</summary>
        public CustomMessagingManager CustomMessagingManager { get; } = new();

        /// <summary>Registers a prefab into <see cref="NetworkConfig"/>'s prefab list.</summary>
        public void AddNetworkPrefab(GameObject prefab) => NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });

        public void RemoveNetworkPrefab(GameObject prefab) => NetworkConfig.Prefabs.Remove(prefab);

        /// <summary>Makes this instance the process singleton (Netcode does this on Awake).</summary>
        public void SetSingleton() => Singleton = this;

        /// <summary>Server-only start: the local peer is the server and no local client exists.</summary>
        public bool StartServer()
        {
            if (IsListening) return false;
            IsServer = true;
            IsClient = false;
            IsListening = true;
            OnServerStarted?.Invoke();
            return true;
        }

        /// <summary>
        /// Client-only start. Offline there is no remote server to reach, so the client
        /// connects to the in-process loopback host: it becomes a listening client with id 0.
        /// </summary>
        public bool StartClient()
        {
            if (IsListening) return false;
            IsServer = false;
            IsClient = true;
            IsListening = true;
            LocalClientId = 0;
            if (!ConnectedClientsIds.Contains(0)) ConnectedClientsIds.Add(0);
            OnClientStarted?.Invoke();
            OnClientConnectedCallback?.Invoke(LocalClientId);
            OnConnectionEvent?.Invoke(this, new ConnectionEventData { ClientId = LocalClientId, EventType = ConnectionEvent.ClientConnected });
            return true;
        }

        /// <summary>Server-side kick: removes the client from the tables and raises the disconnect callback.</summary>
        public void DisconnectClient(ulong clientId, string reason = null)
        {
            ConnectedClientsIds.Remove(clientId);
            if (ConnectedClients.Remove(clientId, out var client))
                ConnectedClientsList.Remove(client);
            if (!string.IsNullOrEmpty(reason)) DisconnectReason = reason;
            NotifyClientDisconnect(clientId);
        }

        /// <summary>
        /// Original-contract host start (single-process host-mode). Runs the local client
        /// (id 0) through <see cref="ConnectionApprovalCallback"/> when one is wired — a
        /// rejection aborts the start, matching Netcode's host self-approval — then flips
        /// the role flags listening. Returns false when already listening or rejected.
        /// </summary>
        public bool StartHost()
        {
            if (IsListening) return false;

            var response = new ConnectionApprovalResponse { Approved = true, CreatePlayerObject = NetworkConfig?.PlayerPrefab != null };
            if (ConnectionApprovalCallback != null && (NetworkConfig == null || NetworkConfig.ConnectionApproval || !EmulateNetcodeLifecycle))
            {
                var request = new ConnectionApprovalRequest { ClientNetworkId = 0, Payload = NetworkConfig?.ConnectionData ?? System.Array.Empty<byte>() };
                response = new ConnectionApprovalResponse();
                ConnectionApprovalCallback(request, response);
                if (!response.Approved) return false;
            }

            IsServer = true;
            IsClient = true;
            IsListening = true;
            LocalClientId = 0;
            if (!ConnectedClientsIds.Contains(0)) ConnectedClientsIds.Add(0);
            OnServerStarted?.Invoke();
            OnClientStarted?.Invoke();
            if (EmulateNetcodeLifecycle && response.CreatePlayerObject) SpawnPlayerObject(0, response);
            OnClientConnectedCallback?.Invoke(LocalClientId);
            OnConnectionEvent?.Invoke(this, new ConnectionEventData { ClientId = LocalClientId, EventType = ConnectionEvent.ClientConnected });
            return true;
        }

        /// <summary>
        /// Original-contract shutdown: stops listening, drops the role flags, and clears the
        /// client tables ("no networking active"). Synchronous — the original's
        /// <c>WaitUntil(() =&gt; !IsListening)</c> completes on its first check. Raises no
        /// callbacks (Netcode's local-notification sweep arrives with the transport phase).
        /// </summary>
        /// <summary>Instantiates and spawns a connected client's player prefab (Netcode does this during approval).</summary>
        void SpawnPlayerObject(ulong clientId, ConnectionApprovalResponse response)
        {
            var prefab = NetworkConfig?.PlayerPrefab;
            if (response.PlayerPrefabHash is uint hash)
                foreach (var p in NetworkConfig.Prefabs.Prefabs)
                    if (p.Prefab != null && p.Prefab.GetComponent<NetworkObject>() is { } no && no.GlobalObjectIdHash == hash) { prefab = p.Prefab; break; }
            if (prefab == null) return;
            var go = Instantiate(prefab, response.Position ?? Vector3.zero, response.Rotation ?? Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null) { Debug.LogError($"[Netcode] Player prefab '{prefab.name}' has no NetworkObject."); return; }
            netObj.SpawnAsPlayerObject(clientId, destroyWithScene: false);
        }

        public void Shutdown(bool discardMessageQueue = false)
        {
            if (EmulateNetcodeLifecycle && IsListening)
            {
                // Netcode despawns (and destroys) every dynamically spawned object on shutdown.
                var spawned = new System.Collections.Generic.List<NetworkObject>(SpawnManager.SpawnedObjectsList);
                foreach (var o in spawned)
                    if (o != null && o.IsSpawned) o.Despawn(destroy: true);
            }
            bool wasServer = IsServer, wasClient = IsClient, wasListening = IsListening;
            bool wasHost = wasServer && wasClient;
            if (wasListening) OnPreShutdown?.Invoke();
            ShutdownInProgress = true;
            IsListening = false;
            IsServer = false;
            IsClient = false;
            ConnectedClients.Clear();
            ConnectedClientsList.Clear();
            ConnectedClientsIds.Clear();
            ShutdownInProgress = false;
            if (wasListening && wasServer) OnServerStopped?.Invoke(wasHost);
            if (wasListening && wasClient) OnClientStopped?.Invoke(wasHost);
        }

        /// <summary>Original-contract approval request (nested in NetworkManager, as in Netcode).</summary>
        public class ConnectionApprovalRequest
        {
            public ulong ClientNetworkId;
            public byte[] Payload;
        }

        /// <summary>Original-contract approval response. Field set + defaults per Netcode 2.x —
        /// approval callbacks assign these; the (future) transport honors them.</summary>
        public class ConnectionApprovalResponse
        {
            public bool Approved;
            public bool CreatePlayerObject;
            public uint? PlayerPrefabHash;
            public Vector3? Position;
            public Quaternion? Rotation;
            public bool Pending;
            public string Reason;
        }
    }

    /// <summary>
    /// Original-contract connection record: the client id plus the player object the server
    /// created for it (engine addition for the vessel-initializer arc).
    /// </summary>
    public class NetworkClient
    {
        public ulong ClientId { get; set; }
        public NetworkObject PlayerObject { get; set; }
    }

    /// <summary>
    /// Original-contract spawned-object registry keyed by <see cref="NetworkObject.NetworkObjectId"/>
    /// (engine addition for the vessel-initializer arc). Maintained by
    /// <see cref="NetworkObject.SpawnWithOwnership"/>/<see cref="NetworkObject.Despawn"/> while a
    /// <see cref="NetworkManager.Singleton"/> is assigned; the harness may also populate it directly.
    /// </summary>
    public class NetworkSpawnManager
    {
        public System.Collections.Generic.Dictionary<ulong, NetworkObject> SpawnedObjects { get; } = new();

        /// <summary>Set view of the spawned objects (original surface).</summary>
        public System.Collections.Generic.HashSet<NetworkObject> SpawnedObjectsList { get; } = new();

        internal void Register(NetworkObject networkObject)
        {
            ulong id = networkObject.NetworkObjectId;
            if (id != 0)
                SpawnedObjects[id] = networkObject;
            SpawnedObjectsList.Add(networkObject);
        }

        internal void Unregister(NetworkObject networkObject)
        {
            ulong id = networkObject.NetworkObjectId;
            if (id != 0)
                SpawnedObjects.Remove(id);
            SpawnedObjectsList.Remove(networkObject);
        }

        /// <summary>The player object owned by <paramref name="clientId"/>, if one was spawned as a player.</summary>
        public NetworkObject GetPlayerNetworkObject(ulong clientId)
        {
            foreach (var obj in SpawnedObjectsList)
                if (obj != null && obj.IsPlayerObject && obj.OwnerClientId == clientId)
                    return obj;
            return null;
        }

        /// <summary>The local client's player object.</summary>
        public NetworkObject GetLocalPlayerObject()
        {
            var nm = NetworkManager.Singleton;
            return GetPlayerNetworkObject(nm == null ? 0UL : nm.LocalClientId);
        }

        /// <summary>Every spawned object owned by <paramref name="clientId"/>.</summary>
        public NetworkObject[] GetClientOwnedObjects(ulong clientId)
        {
            var list = new System.Collections.Generic.List<NetworkObject>();
            foreach (var obj in SpawnedObjectsList)
                if (obj != null && obj.OwnerClientId == clientId)
                    list.Add(obj);
            return list.ToArray();
        }
    }
}
