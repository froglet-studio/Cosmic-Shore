using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-side vessel spawner.
    ///
    /// Flow:
    ///   OnNetworkSpawn → subscribe to OnClientConnectedCallback
    ///   OnClientConnected(clientId) → delay for NetworkVariables
    ///   → spawn vessel → server-side init → notify clients via RPCs
    ///
    /// Persistent Players (surviving Netcode scene loads) are handled by
    /// ProcessPreExistingPlayers(), which triggers the spawn chain for
    /// already-connected clients whose OnClientConnected won't re-fire.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    public class ServerPlayerVesselInitializer : MonoBehaviour
    {
        [Header("Dependencies")]
        [Inject] protected GameDataSO gameData;
        [Inject] protected Container _container;

        [FormerlySerializedAs("clientPlayerSpawner")]
        [SerializeField] protected ClientPlayerVesselInitializer clientPlayerVesselInitializer;

        [SerializeField] protected VesselPrefabContainer vesselPrefabContainer;

        [Header("Lifecycle")]
        [Tooltip("When true, NetworkManager.Shutdown() is called on despawn (game scenes). " +
                 "Set to false for Menu_Main so the host persists across scene transitions.")]
        [SerializeField] protected bool shutdownNetworkOnDespawn = true;

        [Header("Spawn Points")]
        [Tooltip("Scene-placed spawn transforms. If set, overrides GameDataSO.SpawnPoses on network spawn.")]
        [SerializeField] protected Transform[] playerSpawnPoints;

        [Header("Timing")]
        [Tooltip("Delay in ms before spawning a vessel for a newly connected client.")]
        [SerializeField] protected int preSpawnDelayMs = 500;

        [Tooltip("Delay in ms after vessel spawn before notifying clients.")]
        [SerializeField] protected int postSpawnDelayMs = 500;

        NetcodeHooks _netcodeHooks;
        protected CancellationTokenSource _cts;

        /// <summary>
        /// Tracks players already processed (keyed by NetworkObjectId).
        /// Using NetworkObjectId because server-owned AI players share the host's OwnerClientId.
        /// </summary>
        protected readonly HashSet<ulong> _processedPlayers = new();

        protected virtual void Awake()
        {
            _netcodeHooks = GetComponent<NetcodeHooks>();
            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
            _netcodeHooks.OnNetworkDespawnHook += OnNetworkDespawn;
        }

        protected virtual void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_netcodeHooks)
            {
                _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
                _netcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;
            }
        }

        protected virtual void OnNetworkSpawn()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                enabled = false;
                return;
            }

            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                gameData.SetSpawnPositions(playerSpawnPoints);

            _cts = new CancellationTokenSource();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            // Handle persistent Players that survived a Netcode scene load.
            // Their OnClientConnected won't re-fire, so we trigger the spawn chain manually.
            ProcessPreExistingPlayers();
        }

        /// <summary>
        /// Finds already-connected clients whose Player objects persist across scene loads
        /// and triggers the spawn chain for them.
        /// </summary>
        void ProcessPreExistingPlayers()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            foreach (var kvp in nm.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                if (playerObj == null || !playerObj.TryGetComponent<Player>(out var player))
                    continue;
                if (!player.IsSpawned || _processedPlayers.Contains(player.NetworkObjectId))
                    continue;

                OnClientConnected(kvp.Key);
            }
        }

        protected virtual void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            _processedPlayers.Clear();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (shutdownNetworkOnDespawn && NetworkManager.Singleton)
                NetworkManager.Singleton.Shutdown();
        }

        /// <summary>
        /// Called when a new client connects, or manually for persistent Players
        /// via ProcessPreExistingPlayers. Virtual for subclass override (AI spawning).
        /// </summary>
        protected virtual void OnClientConnected(ulong clientId)
        {
            DelayedSpawnVesselForPlayer(clientId).Forget();
        }

        /// <summary>
        /// Waits for NetworkVariables to sync, then spawns a vessel for the player.
        /// Uses SpawnManager.GetPlayerNetworkObject for direct player lookup.
        /// </summary>
        protected async UniTaskVoid DelayedSpawnVesselForPlayer(ulong clientId)
        {
            await UniTask.Delay(preSpawnDelayMs, DelayType.UnscaledDeltaTime, cancellationToken: _cts.Token);

            var playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (!playerNetObj)
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Player object not found for client {clientId}");
                return;
            }

            var player = playerNetObj.GetComponent<Player>();
            if (!player)
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Player component missing on client {clientId}");
                return;
            }

            if (_processedPlayers.Contains(player.NetworkObjectId))
                return;

            // Persistent players need re-initialization for the new scene
            if (player.Vessel == null && !gameData.Players.Contains(player))
                player.PrepareForNewScene();

            // Assign domain if not already set
            if (player.NetDomain.Value is Domains.Unassigned or Domains.None)
                player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);

            if (!_processedPlayers.Add(player.NetworkObjectId))
                return;

            // Fallback vessel type if not set by owner yet
            if (player.NetDefaultVesselType.Value is VesselClassType.Random or VesselClassType.Any)
            {
                CSDebug.LogWarning("[ServerPlayerVesselInitializer] Vessel type not set, defaulting to selected class");
                player.NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;
            }

            await OnPlayerReadyToSpawnAsync(player, _cts.Token);
        }

        /// <summary>
        /// Spawns the vessel, initializes on server, waits for replication, then notifies clients.
        /// Virtual so derived classes (Menu) can add post-init behavior.
        /// </summary>
        protected virtual async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            var vesselNO = SpawnVesselForPlayer(player.OwnerClientId, player);
            if (!vesselNO)
                return;

            if (!vesselNO.TryGetComponent(out IVessel vessel))
            {
                CSDebug.LogError("[ServerPlayerVesselInitializer] Spawned vessel missing IVessel component.");
                return;
            }

            clientPlayerVesselInitializer.InitializePlayerAndVessel(player, vessel);

            // Wait for the vessel NetworkObject to fully replicate before telling clients
            await UniTask.Delay(postSpawnDelayMs, DelayType.UnscaledDeltaTime, cancellationToken: ct);
            NotifyClients(player);
        }

        /// <summary>
        /// Sends RPCs to non-host clients:
        ///   - Existing clients: "initialize just this new pair"
        ///   - New client: "initialize ALL player-vessel pairs"
        /// </summary>
        protected void NotifyClients(Player newPlayer)
        {
            var newClientId = newPlayer.OwnerClientId;
            var hostClientId = NetworkManager.Singleton.LocalClientId;

            // Tell existing non-host clients about the new player-vessel pair
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId == newClientId) continue;
                if (client.ClientId == hostClientId) continue;

                var existingTarget = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
                };
                clientPlayerVesselInitializer.InitializeNewPlayerAndVessel_ClientRpc(
                    newPlayer.PlayerNetId, newPlayer.VesselNetId, existingTarget);
            }

            // Tell the new client to initialize ALL player-vessel pairs
            if (newClientId != hostClientId)
            {
                var playerIds = new List<ulong>();
                var vesselIds = new List<ulong>();
                foreach (var p in gameData.Players)
                {
                    if (p.VesselNetId == 0) continue;
                    playerIds.Add(p.PlayerNetId);
                    vesselIds.Add(p.VesselNetId);
                }

                var newTarget = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { newClientId } }
                };
                clientPlayerVesselInitializer.InitializeAllPlayersAndVessels_ClientRpc(
                    playerIds.ToArray(), vesselIds.ToArray(), newTarget);
            }
        }

        protected NetworkObject SpawnVesselForPlayer(ulong clientId, Player networkPlayer) =>
            SpawnVesselForPlayer(clientId, networkPlayer, networkPlayer.NetDefaultVesselType.Value);

        /// <summary>
        /// Spawns a vessel of the given type, assigns ownership to <paramref name="clientId"/>,
        /// and updates the player's <see cref="Player.NetVesselId"/>.
        /// </summary>
        protected NetworkObject SpawnVesselForPlayer(ulong clientId, Player networkPlayer, VesselClassType vesselType)
        {
            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefabTransform))
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] No prefab for vessel type {vesselType}");
                return null;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return null;
            }

            var networkVessel = Instantiate(shipNetworkObject);
            GameObjectInjector.InjectRecursive(networkVessel.gameObject, _container);
            networkVessel.SpawnWithOwnership(clientId, true);
            networkPlayer.NetVesselId.Value = networkVessel.NetworkObjectId;
            return networkVessel;
        }

        /// <summary>
        /// Despawns and destroys a vessel's <see cref="NetworkObject"/>.
        /// Removes it from <see cref="GameDataSO.Vessels"/> tracking.
        /// </summary>
        protected void DespawnVessel(IVessel vessel)
        {
            gameData.Vessels.Remove(vessel);

            if (vessel is VesselController vc && vc.IsSpawned)
                vc.NetworkObject.Despawn(true);
        }
    }
}
