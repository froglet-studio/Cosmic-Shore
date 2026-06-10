namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Base class for network-aware behaviours, preserving the lifecycle contract the
    /// ported code was written against: <see cref="OnNetworkSpawn"/>/<see cref="OnNetworkDespawn"/>
    /// virtuals plus <see cref="IsSpawned"/>/<see cref="IsServer"/>/<see cref="IsClient"/>/<see cref="IsOwner"/>
    /// role flags. The session/transport layer (networking phase) drives <see cref="Spawn"/>/<see cref="Despawn"/>;
    /// tests and single-process play drive them directly.
    /// </summary>
    public abstract class NetworkBehaviour
    {
        public bool IsSpawned { get; private set; }
        public bool IsServer { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsHost => IsServer && IsClient;
        public bool IsOwner { get; private set; }
        public ulong OwnerClientId { get; private set; }

        public virtual void OnNetworkSpawn() { }
        public virtual void OnNetworkDespawn() { }

        /// <summary>Bring this behaviour into the networked world. Defaults model single-process host-mode.</summary>
        public void Spawn(bool isServer = true, bool isClient = true, bool isOwner = true, ulong ownerClientId = 0)
        {
            IsServer = isServer;
            IsClient = isClient;
            IsOwner = isOwner;
            OwnerClientId = ownerClientId;
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
