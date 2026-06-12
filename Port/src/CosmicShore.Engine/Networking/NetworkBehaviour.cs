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

        /// <summary>Handle exposing <c>Despawn(bool destroy)</c> + the id (E12).</summary>
        public NetworkObject NetworkObject => _networkObject ??= new NetworkObject(this);

        public virtual void OnNetworkSpawn() { }
        public virtual void OnNetworkDespawn() { }

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
        {
            IsServer = isServer;
            IsClient = isClient;
            IsOwner = isOwner;
            OwnerClientId = ownerClientId;
            NetworkObjectId = ++_nextNetworkObjectId;
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
