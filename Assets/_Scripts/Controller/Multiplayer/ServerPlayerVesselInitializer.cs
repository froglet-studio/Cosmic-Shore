using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
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
    ///   OnNetworkSpawn → subscribe to OnPlayerNetworkSpawned
    ///   OnPlayerNetworkSpawned → listen to NetDefaultVesselType.OnValueChanged
    ///   OnValueChanged → spawn vessel → server-side init → RPC to clients
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

        [Header("Spawn Origins")]
        [SerializeField] protected Transform[] _playerOrigins;

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

            SetupSpawnPositions();
            SubscribeAndProcessPlayers();
        }

        protected virtual void OnNetworkDespawn()
        {
            gameData.OnPlayerNetworkSpawned.OnRaised -= HandlePlayerNetworkSpawned;
            _processedPlayers.Clear();

            if (shutdownNetworkOnDespawn && NetworkManager.Singleton)
                NetworkManager.Singleton.Shutdown();
        }

        protected void SetupSpawnPositions()
        {
            gameData.SetSpawnPositions(_playerOrigins);
            DomainAssigner.Initialize();
        }

        protected void SubscribeAndProcessPlayers()
        {
            gameData.OnPlayerNetworkSpawned.OnRaised += HandlePlayerNetworkSpawned;

            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer && _processedPlayers.Add(netPlayer.NetworkObjectId))
                    HandleNewPlayer(netPlayer);
            }
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
        /// If the player's vessel type is already set, spawn immediately.
        /// Otherwise, subscribe to OnValueChanged and spawn when the client sets it.
        /// </summary>
        void HandleNewPlayer(Player player)
        {
            if (IsValidVesselType(player.NetDefaultVesselType.Value))
            {
                OnPlayerReadyToSpawn(player);
                return;
            }

            void OnVesselTypeChanged(VesselClassType _, VesselClassType newVal)
            {
                if (!IsValidVesselType(newVal)) return;
                player.NetDefaultVesselType.OnValueChanged -= OnVesselTypeChanged;
                OnPlayerReadyToSpawn(player);
            }

            player.NetDefaultVesselType.OnValueChanged += OnVesselTypeChanged;
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

        protected void SpawnVesselAndInitialize(ulong clientId, Player player)
        {
            player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
            player.NetIsAI.Value = false;

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

        protected NetworkObject SpawnVesselForPlayer(ulong clientId, Player networkPlayer)
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

        protected Player FindPlayerByClientId(ulong clientId)
        {
            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer && netPlayer.OwnerClientId == clientId)
                    return netPlayer;
            }
            return null;
        }

        static bool IsValidVesselType(VesselClassType type) =>
            type != VesselClassType.Random && type != VesselClassType.Any;
    }
}
