using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-side vessel spawner.
    ///
    /// Flow:
    ///   OnNetworkSpawn → subscribe to OnPlayerNetworkSpawned
    ///   OnPlayerNetworkSpawned → assign domain + AI flag, wait for vessel type AND name
    ///   Both ready → spawn vessel → server-side init → RPC to clients
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

        protected NetcodeHooks _netcodeHooks;

        public Action OnAllPlayersSpawned;

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

            gameData.OnInitializeGame.OnRaised += HandleGameInitialized;
        }

        protected virtual void OnNetworkDespawn()
        {
            gameData.OnPlayerNetworkSpawned.OnRaised -= HandlePlayerNetworkSpawned;
            gameData.OnInitializeGame.OnRaised -= HandleGameInitialized;
            _processedPlayers.Clear();

            if (shutdownNetworkOnDespawn && NetworkManager.Singleton)
                NetworkManager.Singleton.Shutdown();
        }

        protected void SubscribeAndProcessPlayers()
        {
            gameData.OnPlayerNetworkSpawned.OnRaised += HandlePlayerNetworkSpawned;
            HandlePlayerNetworkSpawned();
        }
        
        void HandleGameInitialized()
        {
            gameData.OnInitializeGame.OnRaised -= HandleGameInitialized;
            SubscribeAndProcessPlayers();
        }

        void HandlePlayerNetworkSpawned()
        {
            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer && _processedPlayers.Add(netPlayer.NetworkObjectId))
                    HandleNewPlayer(netPlayer);
            }
        }

        /// <summary>
        /// Assigns domain and AI flag immediately, then waits for both vessel type
        /// and name to be set before proceeding with the spawn chain. This ensures
        /// name and domain are always available when InitializeForMultiplayerMode runs.
        /// </summary>
        void HandleNewPlayer(Player player)
        {
            if (IsReadyToSpawn(player))
            {
                OnPlayerReadyToSpawn(player);
                return;
            }

            void OnVesselTypeChanged(VesselClassType _, VesselClassType newVal)
            {
                if (!IsReadyToSpawn(player)) return;
                player.NetDefaultVesselType.OnValueChanged -= OnVesselTypeChanged;
                player.NetName.OnValueChanged -= OnNameChanged;
                OnPlayerReadyToSpawn(player);
            }

            void OnNameChanged(FixedString128Bytes _, FixedString128Bytes newVal)
            {
                if (!IsReadyToSpawn(player)) return;
                player.NetDefaultVesselType.OnValueChanged -= OnVesselTypeChanged;
                player.NetName.OnValueChanged -= OnNameChanged;
                OnPlayerReadyToSpawn(player);
            }

            player.NetDefaultVesselType.OnValueChanged += OnVesselTypeChanged;
            player.NetName.OnValueChanged += OnNameChanged;
        }

        /// <summary>
        /// Called when a player's vessel type is confirmed.
        /// Spawns the vessel, initializes on server, and notifies clients via RPCs.
        /// Virtual so derived classes (Menu) can add post-init behavior.
        /// </summary>
        protected virtual void OnPlayerReadyToSpawn(Player player)
        {
            SpawnVesselAndInitialize(player.OwnerClientId, player);
            NotifyClients(player);
            gameData.InvokeClientReady();
        }

        void SpawnVesselAndInitialize(ulong clientId, Player player)
        {
            var vesselNO = SpawnVesselForPlayer(clientId, player);
            if (vesselNO == null)
                return;

            clientPlayerVesselInitializer.InitializePlayerAndVessel(player, vesselNO);
            OnAllPlayersSpawned?.Invoke();
        }

        /// <summary>
        /// Sends RPCs to non-host clients:
        ///   - New client: "initialize ALL player-vessel pairs"
        ///   - Existing clients: "initialize just this new pair"
        /// </summary>
        void NotifyClients(Player newPlayer)
        {
            var newClientId = newPlayer.OwnerClientId;
            var hostClientId = NetworkManager.Singleton.LocalClientId;

            // Collect ALL player-vessel pairs for the "init all" RPC
            var playerIds = new List<ulong>();
            var vesselIds = new List<ulong>();
            foreach (var p in gameData.Players)
            {
                if (p.VesselNetId == 0) continue;
                playerIds.Add(p.PlayerNetId);
                vesselIds.Add(p.VesselNetId);
            }

            // To new client: initialize ALL pairs (host + AI + other clients + self)
            if (newClientId != hostClientId)
            {
                var newTarget = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { newClientId } }
                };
                clientPlayerVesselInitializer.InitializeAllPlayersAndVessels_ClientRpc(
                    playerIds.ToArray(), vesselIds.ToArray(), newTarget);
            }

            // To existing non-host clients: initialize just the new pair
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
        /// A player is ready to spawn when both vessel type and name are set.
        /// For the host, both are written in OnNetworkSpawn before the event fires.
        /// For remote clients, NetworkVariable changes may arrive in separate ticks,
        /// so we wait for both.
        /// </summary>
        bool IsReadyToSpawn(Player player) =>
            IsValidVesselType(player.NetDefaultVesselType.Value)
            && !string.IsNullOrEmpty(player.NetName.Value.ToString());

        static bool IsValidVesselType(VesselClassType type) =>
            type != VesselClassType.Random && type != VesselClassType.Any;
    }
}
