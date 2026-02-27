using System;
using System.Collections.Generic;
using CosmicShore.Data;
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
    ///   OnNetworkSpawn → subscribe to OnPlayerNetworkSpawned
    ///   OnPlayerNetworkSpawned → wait for NetDefaultVesselType → spawn vessel → initialize → ClientReady
    ///
    /// Handles both the host player and late-joining remote clients uniformly.
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
        /// Tracks players that have already been processed (keyed by NetworkObjectId).
        /// Using NetworkObjectId instead of OwnerClientId because server-owned AI players
        /// share the host's OwnerClientId.
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

        /// <summary>
        /// Initializes spawn positions and domain assignment. Safe to call once.
        /// </summary>
        protected void SetupSpawnPositions()
        {
            gameData.SetSpawnPositions(_playerOrigins);
            DomainAssigner.Initialize();
        }

        /// <summary>
        /// Subscribes to OnPlayerNetworkSpawned and processes any players already
        /// present in gameData. Call after any pre-spawn work (e.g., AI spawning).
        /// </summary>
        protected void SubscribeAndProcessPlayers()
        {
            gameData.OnPlayerNetworkSpawned.OnRaised += HandlePlayerNetworkSpawned;

            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer && _processedPlayers.Add(netPlayer.NetworkObjectId))
                    SpawnVesselWhenReady(netPlayer).Forget();
            }
        }

        void HandlePlayerNetworkSpawned()
        {
            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer && _processedPlayers.Add(netPlayer.NetworkObjectId))
                    SpawnVesselWhenReady(netPlayer).Forget();
            }
        }

        /// <summary>
        /// Waits for the player's NetDefaultVesselType to be set (by the owning client),
        /// then spawns a vessel and initializes the player-vessel pair.
        ///
        /// For the host player this resolves within the same frame (set in Player.OnNetworkSpawn).
        /// For remote clients this waits for network variable replication.
        /// </summary>
        protected virtual async UniTask SpawnVesselWhenReady(Player player)
        {
            var ct = this.GetCancellationTokenOnDestroy();

            // Player.OnNetworkSpawn raises OnPlayerNetworkSpawned BEFORE setting
            // NetDefaultVesselType, so we always need at least a yield.
            await UniTask.WaitUntil(
                () => player.NetDefaultVesselType.Value != VesselClassType.Random
                   && player.NetDefaultVesselType.Value != VesselClassType.Any,
                cancellationToken: ct);

            SpawnVesselAndInitialize(player.OwnerClientId, player);
        }

        protected void SpawnVesselAndInitialize(ulong clientId, Player player)
        {
            player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
            player.NetIsAI.Value = false;

            var vesselNO = SpawnVesselForPlayer(clientId, player);
            if (vesselNO == null)
                return;

            // Server-side initialization (runs on host)
            clientPlayerVesselInitializer.InitializePlayerAndVessel(player, vesselNO);

            OnAllPlayersSpawned?.Invoke();
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
    }
}
