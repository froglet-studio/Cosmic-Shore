using System;
using System.Collections.Generic;
using CosmicShore.Soap;
using CosmicShore.SOAP;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Multiplayer.Samples.Utilities;
using CosmicShore.Utility.ClassExtensions;
using Obvious.Soap;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    /// <summary>
    /// Server-side system: Spawns networked vessels and keeps them synced with player prefab state.
    /// Handles new clients, vessel swaps, and late-join sync.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    public class ServerPlayerVesselInitializer : MonoBehaviour
    {
        [SerializeField] MiniGameDataSO gameData;
        [FormerlySerializedAs("clientPlayerSpawner")] [SerializeField] 
        ClientPlayerVesselInitializer clientPlayerVesselInitializer;
        [SerializeField] 
        VesselPrefabContainer vesselPrefabContainer;

        NetcodeHooks _netcodeHooks;
        List<Transform> _playerSpawnPointsList = null;
        
        public Action OnAllPlayersSpawned;

        private void Awake()
        {
            _netcodeHooks = GetComponent<NetcodeHooks>();
            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
            _netcodeHooks.OnNetworkDespawnHook += OnNetworkDespawn;
        }

        private void OnNetworkSpawn()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                enabled = false;
                return;
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkVesselClientCache.OnNewInstanceAdded += OnNewVesselClientAdded;
        }

        private void OnNetworkDespawn()
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkVesselClientCache.OnNewInstanceAdded -= OnNewVesselClientAdded;
            
            NetworkManager.Singleton.Shutdown();
        }

        private void OnDestroy()
        {
            if (_netcodeHooks)
            {
                _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
                _netcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;
            }
        }

        // ----------------------------
        // Lobby full check
        // ----------------------------
        void OnNewVesselClientAdded(IVessel _)
        {
            var session = MultiplayerSetup.Instance.ActiveSession;
            if (session is { AvailableSlots: 0 })
            {
                DebugExtensions.LogColored("[ServerPlayerVesselInitializer] All players have joined; lobby full.", Color.green);
                OnAllPlayersSpawned?.Invoke();
            }
        }

        // ----------------------------
        // New client connected
        // ----------------------------
        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[ServerPlayerVesselInitializer] Client {clientId} connected → syncing vessels.");

            // Then spawn their own vessel after 2s
            DelayedSpawnVesselForPlayer(clientId).Forget();
            
            clientPlayerVesselInitializer.InvokeClientReady_ClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            });
        }

        // ----------------------------
        // Spawn vessel for a new client after 2s
        // ----------------------------
        private async UniTaskVoid DelayedSpawnVesselForPlayer(ulong clientId)
        {
            try
            {
                await UniTask.Delay(500, DelayType.UnscaledDeltaTime);

                var playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (!playerNetObj)
                {
                    Debug.LogError($"[ServerPlayerVesselInitializer] Player object not found for client {clientId}");
                    return;
                }

                var player = playerNetObj.GetComponent<Player>();
                if (!player)
                {
                    Debug.LogError($"[ServerPlayerVesselInitializer] Player component missing on {clientId}");
                    return;
                }

                // Spawn initial vessel if type already chosen
                if (player.NetDefaultShipType.Value != VesselClassType.Random)
                {
                    SpawnVesselForPlayer(clientId, player);
                }
                
                await UniTask.Delay(500, DelayType.UnscaledDeltaTime);

                foreach (var clientPair in NetworkManager.Singleton.ConnectedClientsList)
                {
                    var target = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { clientId }
                    };
                    
                    if (clientPair.ClientId != clientId)
                    {
                        clientPlayerVesselInitializer.InitializePlayerAndVessel_ClientRpc(clientId, new ClientRpcParams
                        {
                            Send = target
                        });
                    }
                    else
                    {
                        clientPlayerVesselInitializer.InitializeAllPlayersAndVessels_ClientRpc(new ClientRpcParams
                        {
                            Send = target
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerPlayerVesselInitializer] Error in DelayedSpawnVesselForPlayer: {ex}");
            }
        }

        // ----------------------------
        // Vessel spawning logic
        // ----------------------------
        private void SpawnVesselForPlayer(ulong clientId, Player networkPlayer)
        {
            VesselClassType vesselTypeToSpawn = networkPlayer.NetDefaultShipType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselTypeToSpawn, out Transform shipPrefabTransform))
            {
                Debug.LogError($"[ServerPlayerVesselInitializer] No prefab for vessel type {vesselTypeToSpawn}");
                return;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                Debug.LogError($"[ServerPlayerVesselInitializer] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return;
            }

            var networkShip = Instantiate(shipNetworkObject);
            networkShip.SpawnWithOwnership(clientId, true);
            
            Transform spawnPoint = GetRandomSpawnPoint();
            if (spawnPoint != null)
                networkShip.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

            Debug.Log($"[ServerPlayerVesselInitializer] Spawned {vesselTypeToSpawn} for client {clientId}");
        }

        // ----------------------------
        // Spawn point picker
        // ----------------------------
        private Transform GetRandomSpawnPoint()
        {
            if (gameData.PlayerOrigins == null || gameData.PlayerOrigins.Length == 0)
            {
                Debug.LogError("[ServerPlayerVesselInitializer] PlayerSpawnPoints array not set or empty.");
                return null;
            }

            if (_playerSpawnPointsList == null || _playerSpawnPointsList.Count == 0)
                _playerSpawnPointsList = new List<Transform>(gameData.PlayerOrigins);

            int index = UnityEngine.Random.Range(0, _playerSpawnPointsList.Count);
            Transform spawnPoint = _playerSpawnPointsList[index];
            _playerSpawnPointsList.RemoveAt(index);
            return spawnPoint;
        }
    }
}
