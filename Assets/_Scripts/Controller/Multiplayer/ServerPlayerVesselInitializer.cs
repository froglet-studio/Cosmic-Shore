using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.UniTask;
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
    ///   OnNetworkSpawn → subscribe to OnPlayerNetworkSpawnedUlong
    ///   OnPlayerNetworkSpawnedUlong(ownerClientId) → wait for NetworkVariables to sync
    ///   → spawn vessel → server-side init
    ///   → wait → notify existing clients about new player
    ///   → notify new client about all players
    ///
    /// RPCs:
    ///   New client   → InitializeAllPlayersAndVessels_ClientRpc (all pairs)
    ///   Existing clients → InitializeNewPlayerAndVessel_ClientRpc (just the new pair)
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
        [SerializeField] bool shutdownNetworkOnDespawn = true;

        [Header("Timing")]
        [Tooltip("Delay in ms after OnPlayerNetworkSpawned before reading NetworkVariables.")]
        [SerializeField] protected int preSpawnDelayMs = 200;

        [Tooltip("Delay in ms after vessel spawn before notifying clients.")]
        [SerializeField] protected int postSpawnDelayMs = 200;

        NetcodeHooks _netcodeHooks;
        CancellationTokenSource _cts;

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

            _cts = new CancellationTokenSource();
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised += HandlePlayerNetworkSpawned;
        }

        protected virtual void OnNetworkDespawn()
        {
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised -= HandlePlayerNetworkSpawned;
            _processedPlayers.Clear();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (shutdownNetworkOnDespawn && NetworkManager.Singleton)
                NetworkManager.Singleton.Shutdown();
        }

        /// <summary>
        /// Called when a Player's OnNetworkSpawn fires. The ownerClientId
        /// identifies which client owns this player. We wait a short delay
        /// for NetworkVariables (NetDomain, NetDefaultVesselType, NetIsAI, NetName)
        /// to replicate, then proceed with vessel spawning.
        /// </summary>
        void HandlePlayerNetworkSpawned(ulong ownerClientId)
        {
            HandlePlayerNetworkSpawnedAsync(ownerClientId, _cts.Token).Forget();
        }

        async UniTaskVoid HandlePlayerNetworkSpawnedAsync(ulong ownerClientId, CancellationToken ct)
        {
            // Wait for NetworkVariables set in Player.OnNetworkSpawn to sync
            await UniTask.Delay(preSpawnDelayMs, cancellationToken: ct);

            Player player = FindUnprocessedPlayerByOwnerClientId(ownerClientId);
            if (player == null)
                return;

            if (!_processedPlayers.Add(player.NetworkObjectId))
                return;

            if (!IsReadyToSpawn(player))
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Player {ownerClientId} not ready after delay. " +
                                 $"VesselType={player.NetDefaultVesselType.Value}, Name={player.NetName.Value}");
                return;
            }

            await OnPlayerReadyToSpawnAsync(player, ct);
        }

        /// <summary>
        /// Called when a player's vessel type is confirmed.
        /// Spawns the vessel, initializes on server, waits, then notifies clients via RPCs.
        /// Virtual so derived classes (Menu) can add post-init behavior.
        /// </summary>
        protected virtual async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            SpawnVesselAndInitialize(player.OwnerClientId, player);

            // Wait for the vessel NetworkObject to fully replicate before telling clients
            await UniTask.Delay(postSpawnDelayMs, cancellationToken: ct);

            NotifyClients(player);
        }

        void SpawnVesselAndInitialize(ulong clientId, Player player)
        {
            var vesselNO = SpawnVesselForPlayer(clientId, player);
            if (vesselNO == null)
                return;

            clientPlayerVesselInitializer.InitializePlayerAndVessel(player, vesselNO);
        }

        /// <summary>
        /// Sends RPCs to non-host clients:
        ///   - Existing clients: "initialize just this new pair"
        ///   - New client: "initialize ALL player-vessel pairs"
        /// </summary>
        void NotifyClients(Player newPlayer)
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

        NetworkObject SpawnVesselForPlayer(ulong clientId, Player networkPlayer)
        {
            var vesselType = networkPlayer.NetDefaultVesselType.Value;

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
        /// Finds the first unprocessed Player owned by the given clientId.
        /// </summary>
        Player FindUnprocessedPlayerByOwnerClientId(ulong ownerClientId)
        {
            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer
                    && netPlayer.OwnerClientId == ownerClientId
                    && !_processedPlayers.Contains(netPlayer.NetworkObjectId))
                {
                    return netPlayer;
                }
            }
            return null;
        }

        /// <summary>
        /// A player is ready to spawn when both vessel type and name are set.
        /// </summary>
        protected bool IsReadyToSpawn(Player player) =>
            IsValidVesselType(player.NetDefaultVesselType.Value)
            && !string.IsNullOrEmpty(player.NetName.Value.ToString());

        protected static bool IsValidVesselType(VesselClassType type) =>
            type != VesselClassType.Random && type != VesselClassType.Any;
    }
}
