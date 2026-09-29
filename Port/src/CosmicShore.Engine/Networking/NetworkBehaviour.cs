namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Base class for network-aware behaviours, preserving the lifecycle contract the
    /// ported code was written against: <see cref="OnNetworkSpawn"/>/<see cref="OnNetworkDespawn"/>
    /// virtuals plus <see cref="IsSpawned"/>/<see cref="IsServer"/>/<see cref="IsClient"/>/<see cref="IsOwner"/>
    /// role flags. The session/transport layer (networking phase) drives <see cref="Spawn"/>/<see cref="Despawn"/>;
    /// tests and single-process play drive them directly.
    /// </summary>
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        // E12: process-wide monotonic id source; 0 is the "never spawned" sentinel.
        static ulong _nextNetworkObjectId;

        NetworkObject _networkObject;

        public bool IsSpawned { get; private set; }
        public bool IsServer { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsHost => IsServer && IsClient;
        public bool IsOwner { get; private set; }
        public ulong OwnerClientId { get; private set; }

        /// <summary>
        /// Replication id, allocated monotonically at <see cref="Spawn"/> (E12).
        /// 0 until first spawn; retains the last allocated id after despawn so
        /// late log lines / lookups stay meaningful, matching the original engine.
        /// </summary>
        public ulong NetworkObjectId { get; private set; }

        /// <summary>
        /// The object-level <see cref="Networking.NetworkObject"/> component (E12 handle →
        /// full component in the vessel-initializer arc): resolves the authored NetworkObject
        /// on this GameObject, lazily adding one for behaviours spawned directly — preserving
        /// the handle contract (<c>NetworkObject.Despawn(true)</c> despawns + destroys).
        /// </summary>
        public NetworkObject NetworkObject
        {
            get
            {
                if (_networkObject == null)
                {
                    _networkObject = GetComponent<NetworkObject>();
                    if (_networkObject == null)
                        _networkObject = gameObject.AddComponent<NetworkObject>();
                }
                return _networkObject;
            }
        }

        public virtual void OnNetworkSpawn() { }
        public virtual void OnNetworkDespawn() { }

        // ── Netcode 2.x surface for the live game code (offline single-process host) ──

        /// <summary>The NetworkManager this behaviour belongs to — the process singleton offline.</summary>
        public NetworkManager NetworkManager => NetworkManager.Singleton;

        /// <summary>True when this behaviour's object is the local client's player object.</summary>
        public bool IsLocalPlayer => IsSpawned && IsOwner && NetworkObject.IsPlayerObject;

        /// <summary>True when the owner is the server (client id 0).</summary>
        public bool IsOwnedByServer => OwnerClientId == NetworkManager.ServerClientId;

        /// <summary>Client-server topology: the server holds authority.</summary>
        public bool HasAuthority => IsServer;

        /// <summary>True when the local peer is the session owner (the host, offline).</summary>
        public bool IsSessionOwner => IsServer;

        /// <summary>Index of this behaviour among its object's behaviours.</summary>
        public ushort NetworkBehaviourId
        {
            get
            {
                var all = gameObject.GetComponentsInChildren<NetworkBehaviour>(includeInactive: true);
                for (int i = 0; i < all.Length; i++)
                    if (ReferenceEquals(all[i], this)) return (ushort)i;
                return 0;
            }
        }

        /// <summary>RPC target helper (Rpc/SendTo API). Local invocation semantics offline.</summary>
        public RpcTarget RpcTarget { get; } = new RpcTarget();

        /// <summary>Invoked after <see cref="OnNetworkSpawn"/> on every behaviour of the object.</summary>
        protected virtual void OnNetworkPostSpawn() { }

        /// <summary>Invoked when the local peer gains ownership of this object.</summary>
        public virtual void OnGainedOwnership() { }

        /// <summary>Invoked when the local peer loses ownership of this object.</summary>
        public virtual void OnLostOwnership() { }

        /// <summary>Invoked on every ownership change with the previous and current owner.</summary>
        protected virtual void OnOwnershipChanged(ulong previous, ulong current) { }

        /// <summary>Invoked when the object is re-parented under another NetworkObject.</summary>
        public virtual void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject) { }

        /// <summary>Invoked after in-scene objects finish spawning (scene synchronization).</summary>
        protected virtual void OnInSceneObjectsSpawned() { }

        /// <summary>Invoked when a late-joining client finishes synchronizing.</summary>
        protected virtual void OnNetworkSessionSynchronized() { }

        /// <summary>Resolves a spawned NetworkObject by id through the singleton's spawn manager.</summary>
        protected NetworkObject GetNetworkObject(ulong networkId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            return nm.SpawnManager.SpawnedObjects.TryGetValue(networkId, out var obj) ? obj : null;
        }

        /// <summary>Marks replicated state dirty — replication is local offline, so a no-op.</summary>
        public void SetDirty(bool dirty) { }

        internal void RunPostSpawn() => OnNetworkPostSpawn();

        /// <summary>
        /// Ownership transfer (driven by <see cref="Networking.NetworkObject.ChangeOwnership"/>):
        /// updates the owner id/flag and raises the gained/lost/changed callbacks.
        /// </summary>
        internal void ApplyOwnership(ulong newOwner, ulong localClientId)
        {
            ulong previous = OwnerClientId;
            bool wasOwner = IsOwner;
            OwnerClientId = newOwner;
            IsOwner = newOwner == localClientId;
            if (!IsSpawned) return;
            if (wasOwner && !IsOwner) OnLostOwnership();
            if (!wasOwner && IsOwner) OnGainedOwnership();
            if (previous != newOwner) OnOwnershipChanged(previous, newOwner);
        }

        /// <summary>
        /// E17 (C4): virtual destroy hook matching the original Netcode NetworkBehaviour
        /// surface — ported subclasses write <c>public override void OnDestroy()</c>
        /// (VesselController, Player). The MonoBehaviour lifecycle discovers the
        /// most-derived declaration reflectively and invokes it on destruction; this
        /// base declaration only provides the override target and is a no-op.
        /// </summary>
        public virtual void OnDestroy() { }

        /// <summary>Bring this behaviour into the networked world. Defaults model single-process host-mode.</summary>
        public void Spawn(bool isServer = true, bool isClient = true, bool isOwner = true, ulong ownerClientId = 0)
            => SpawnWithId(AllocateObjectId(), isServer, isClient, isOwner, ownerClientId);

        /// <summary>Allocates a fresh replication id (shared source for behaviours and whole-object spawns).</summary>
        internal static ulong AllocateObjectId() => ++_nextNetworkObjectId;

        /// <summary>
        /// Spawn carrying an externally-allocated id. <see cref="NetworkObject.SpawnWithOwnership"/>
        /// uses this to give every behaviour of one object the SAME id — the original engine's
        /// object-level id contract (a behaviour's NetworkObjectId is its object's id).
        /// </summary>
        internal void SpawnWithId(ulong networkObjectId, bool isServer, bool isClient, bool isOwner, ulong ownerClientId)
        {
            IsServer = isServer;
            IsClient = isClient;
            IsOwner = isOwner;
            OwnerClientId = ownerClientId;
            NetworkObjectId = networkObjectId;
            IsSpawned = true;
            OnNetworkSpawn();
        }

        public void Despawn()
        {
            if (!IsSpawned) return;
            OnNetworkDespawn();
            IsSpawned = false;
        }
    }
}
