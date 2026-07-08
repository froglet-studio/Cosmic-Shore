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
        public void NotifyClientDisconnect(ulong clientId) => OnClientDisconnectCallback?.Invoke(clientId);
        public void NotifyTransportFailure() => OnTransportFailure?.Invoke();

        /// <summary>
        /// Original-contract host start (single-process host-mode). Runs the local client
        /// (id 0) through <see cref="ConnectionApprovalCallback"/> when one is wired — a
        /// rejection aborts the start, matching Netcode's host self-approval — then flips
        /// the role flags listening. Returns false when already listening or rejected.
        /// </summary>
        public bool StartHost()
        {
            if (IsListening) return false;

            if (ConnectionApprovalCallback != null)
            {
                var request = new ConnectionApprovalRequest { ClientNetworkId = 0, Payload = System.Array.Empty<byte>() };
                var response = new ConnectionApprovalResponse();
                ConnectionApprovalCallback(request, response);
                if (!response.Approved) return false;
            }

            IsServer = true;
            IsClient = true;
            IsListening = true;
            LocalClientId = 0;
            if (!ConnectedClientsIds.Contains(0)) ConnectedClientsIds.Add(0);
            return true;
        }

        /// <summary>
        /// Original-contract shutdown: stops listening, drops the role flags, and clears the
        /// client tables ("no networking active"). Synchronous — the original's
        /// <c>WaitUntil(() =&gt; !IsListening)</c> completes on its first check. Raises no
        /// callbacks (Netcode's local-notification sweep arrives with the transport phase).
        /// </summary>
        public void Shutdown()
        {
            IsListening = false;
            IsServer = false;
            IsClient = false;
            ConnectedClients.Clear();
            ConnectedClientsList.Clear();
            ConnectedClientsIds.Clear();
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

        internal void Register(NetworkObject networkObject)
        {
            ulong id = networkObject.NetworkObjectId;
            if (id != 0)
                SpawnedObjects[id] = networkObject;
        }

        internal void Unregister(NetworkObject networkObject)
        {
            ulong id = networkObject.NetworkObjectId;
            if (id != 0)
                SpawnedObjects.Remove(id);
        }
    }
}
