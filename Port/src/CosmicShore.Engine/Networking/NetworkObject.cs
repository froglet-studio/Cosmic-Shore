namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Handle a <see cref="NetworkBehaviour"/> exposes as its <c>NetworkObject</c>
    /// (E12). Ported code reaches through it for the replication id and for
    /// <see cref="Despawn"/>'s destroy-on-despawn overload
    /// (<c>NetworkObject.Despawn(true)</c> in <c>Player.DestroyPlayer</c> /
    /// <c>VesselController.DestroyVessel</c>). The full multi-behaviour
    /// NetworkObject component arrives with the multiplayer-spawn arc; until then
    /// each behaviour owns one handle.
    /// </summary>
    public sealed class NetworkObject
    {
        readonly NetworkBehaviour _behaviour;

        internal NetworkObject(NetworkBehaviour behaviour) => _behaviour = behaviour;

        /// <summary>Replication id of the owning behaviour (allocated at Spawn).</summary>
        public ulong NetworkObjectId => _behaviour.NetworkObjectId;

        public bool IsSpawned => _behaviour.IsSpawned;

        /// <summary>
        /// Remove the behaviour from the networked world; with <paramref name="destroy"/>
        /// true also destroy its GameObject (deferred, end-of-frame — the engine's
        /// standard Destroy contract).
        /// </summary>
        public void Despawn(bool destroy = true)
        {
            _behaviour.Despawn();
            if (destroy) Object.Destroy(_behaviour.gameObject);
        }
    }
}
