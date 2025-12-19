using System;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Multiplayer.Samples.Utilities;
using CosmicShore.Utility.ClassExtensions;
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
        [SerializeField] GameDataSO gameData;
        [FormerlySerializedAs("clientPlayerSpawner")] [SerializeField] 
        ClientPlayerVesselInitializer clientPlayerVesselInitializer;
        [SerializeField] 
        VesselPrefabContainer vesselPrefabContainer;

        [SerializeField]
        Transform[] _playerOrigins;
        
        NetcodeHooks _netcodeHooks;
        
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
            
            gameData.SetSpawnPositions(_playerOrigins);
            
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
            var session = gameData.ActiveSession;
            if (session is { AvailableSlots: 0 })
            {
                // DebugExtensions.LogColored("[ServerPlayerVesselInitializer] All players have joined; lobby full.", Color.green);
                OnAllPlayersSpawned?.Invoke();
            }
        }

        // ----------------------------
        // New client connected
        // ----------------------------
        private void OnClientConnected(ulong clientId)
        {
            SpawnVesselAndInitializeWithPlayer(clientId);
        }
        
        void SpawnVesselAndInitializeWithPlayer(ulong clientId)
        {
            Debug.Log($"[ServerPlayerVesselInitializer] Client {clientId} connected → syncing vessels.");

            // Then spawn their own vessel after 2s
            DelayedSpawnVesselForPlayer(clientId).Forget();
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
                    // A new client joined in this client, we need to initialize the new client's vessel and player clone only.
                    if (clientPair.ClientId != clientId)
                    {
                        var target = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { clientPair.ClientId }
                        };
                        
                        clientPlayerVesselInitializer.InitializeNewPlayerAndVesselInThisClient_ClientRpc(clientId, new ClientRpcParams
                        {
                            Send = target
                        });
                    }
                    // This is the new client, and we have to initialize all the other client's vessel and player clones in this client.
                    else
                    {
                        var target = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { clientId }
                        };
                        
                        clientPlayerVesselInitializer.InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(new ClientRpcParams
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

            /*if (!networkShip.TryGetComponent(out IVessel vessel))
            {
                Debug.LogError("Network Vessel must have IVessel component");
                return;
            }
            
            vessel.SetPose(gameData.GetRandomSpawnPose());*/

            // Debug.Log($"[ServerPlayerVesselInitializer] Spawned {vesselTypeToSpawn} for client {clientId}");
        }
    }
}
