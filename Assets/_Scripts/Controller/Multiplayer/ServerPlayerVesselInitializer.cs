using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-side system: on network spawn, finds the host player,
    /// spawns a vessel, initializes both, and signals client ready.
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

        [Header("Spawn Origins")]
        [SerializeField] protected Transform[] _playerOrigins;

        protected NetcodeHooks _netcodeHooks;

        public Action OnAllPlayersSpawned;

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

            gameData.SetSpawnPositions(_playerOrigins);
            DomainAssigner.Initialize();

            var hostClientId = NetworkManager.Singleton.LocalClientId;
            var player = FindPlayerByClientId(hostClientId);
            if (player == null)
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Host player not found for client {hostClientId}. " +
                                 $"Players registered: {gameData.Players.Count}");
                return;
            }

            SpawnVesselAndInitialize(hostClientId, player);
        }

        protected virtual void OnNetworkDespawn() { }

        protected void SpawnVesselAndInitialize(ulong clientId, Player player)
        {
            player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
            player.NetIsAI.Value = false;

            if (player.NetDefaultVesselType.Value == VesselClassType.Random)
            {
                CSDebug.LogWarning("[ServerPlayerVesselInitializer] Vessel type not set, defaulting to Dolphin.");
                player.NetDefaultVesselType.Value = VesselClassType.Dolphin;
            }

            var vesselNO = SpawnVesselForPlayer(clientId, player);
            if (vesselNO == null)
                return;

            clientPlayerVesselInitializer.InitializePlayerAndVessel(player, vesselNO);

            gameData.InvokeClientReady();
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
