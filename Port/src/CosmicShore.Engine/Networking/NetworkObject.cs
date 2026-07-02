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
    /// on its GameObject and children, and reports the FIRST behaviour's id as the object id —
    /// the original shares one id across an object's behaviours; ported prefabs list their
    /// primary behaviour (e.g. <c>VesselController</c>, <c>Player</c>) first, so lookups keyed on
    /// <c>IVessel.VesselNetId</c> / <c>IPlayer.PlayerNetId</c> resolve identically.
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

        NetworkBehaviour[] Behaviours => gameObject.GetComponentsInChildren<NetworkBehaviour>(includeInactive: true);

        /// <summary>
        /// Replication id of the object — the first SPAWNED behaviour's id in component
        /// order (falling back to the first behaviour's retained id, 0 when never spawned).
        /// Ported prefabs list their primary behaviour (VesselController, Player) before
        /// secondary ones, so a whole-object spawn reports the primary id.
        /// </summary>
        public ulong NetworkObjectId
        {
            get
            {
                var behaviours = Behaviours;
                foreach (var behaviour in behaviours)
                    if (behaviour.IsSpawned)
                        return behaviour.NetworkObjectId;
                return behaviours.Length > 0 ? behaviours[0].NetworkObjectId : 0UL;
            }
        }

        /// <summary>True while any behaviour on the object is spawned.</summary>
        public bool IsSpawned
        {
            get
            {
                foreach (var behaviour in Behaviours)
                    if (behaviour.IsSpawned)
                        return true;
                return false;
            }
        }

        /// <summary>Spawn with local ownership (original signature).</summary>
        public void Spawn(bool destroyWithScene = false)
            => SpawnWithOwnership(LocalClientId, destroyWithScene);

        /// <summary>
        /// Spawn every NetworkBehaviour on this object (and children) with ownership assigned to
        /// <paramref name="ownerClientId"/>, all sharing ONE freshly-allocated object id — the
        /// original engine's contract (every behaviour's NetworkObjectId is its object's id), so
        /// id round-trips like <c>player.NetVesselId = vesselObject.NetworkObjectId</c> →
        /// <c>GameDataSO.TryGetVesselByNetworkObjectId</c> resolve regardless of component order.
        /// Role flags come from the active <see cref="NetworkManager.Singleton"/>; absent one,
        /// single-process host-mode defaults apply (matching <see cref="NetworkBehaviour.Spawn"/>).
        /// Registers with the manager's <see cref="NetworkManager.SpawnManager"/> when one exists.
        /// </summary>
        public void SpawnWithOwnership(ulong ownerClientId, bool destroyWithScene = false)
        {
            DestroyWithScene = destroyWithScene;

            var nm = NetworkManager.Singleton;
            bool isServer = nm == null || nm.IsServer;
            bool isClient = nm == null || nm.IsClient;
            bool isOwner = ownerClientId == LocalClientId;
            ulong objectId = NetworkBehaviour.AllocateObjectId();

            foreach (var behaviour in Behaviours)
                if (!behaviour.IsSpawned)
                    behaviour.SpawnWithId(objectId, isServer, isClient, isOwner, ownerClientId);

            if (nm != null)
                nm.SpawnManager?.Register(this);
        }

        /// <summary>
        /// Despawn every NetworkBehaviour on this object (and children); with
        /// <paramref name="destroy"/> true also destroy the GameObject (deferred, end-of-frame —
        /// the engine's standard Destroy contract).
        /// </summary>
        public void Despawn(bool destroy = true)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
                nm.SpawnManager?.Unregister(this);

            foreach (var behaviour in Behaviours)
                behaviour.Despawn();

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
