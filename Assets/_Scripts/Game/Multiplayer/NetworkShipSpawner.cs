using System;
using System.Collections.Generic;
using CosmicShore.Soap;
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
    public class NetworkShipSpawner : MonoBehaviour
    {
        [SerializeField] ScriptableEventNoParam OnAllClientJoined;
        [SerializeField] ClientPlayerSpawner clientPlayerSpawner;
        [SerializeField, Tooltip("A collection of locations for spawning players")]
        Transform[] _playerSpawnPoints;
        [FormerlySerializedAs("shipPrefabContainer")] 
        [SerializeField] VesselPrefabContainer vesselPrefabContainer;

        NetcodeHooks _netcodeHooks;
        List<Transform> _playerSpawnPointsList = null;

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
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            NetworkVesselClientCache.OnNewInstanceAdded -= OnNewVesselClientAdded;

            if (_netcodeHooks.IsServer)
                NetworkManager.Singleton.Shutdown();
        }

        private void OnDestroy()
        {
            if (_netcodeHooks != null)
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
            if (session != null && session.AvailableSlots == 0)
            {
                DebugExtensions.LogColored("[NetworkShipSpawner] All players have joined; lobby full.", Color.green);
                OnAllClientJoined?.Raise();
            }
        }

        // ----------------------------
        // New client connected
        // ----------------------------
        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            Debug.Log($"[NetworkShipSpawner] Client {clientId} connected → syncing vessels.");

            // Sync ALL existing vessels to the newcomer
            SyncExistingPlayersForNewClient(clientId);

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
                await UniTask.Delay(2000, DelayType.UnscaledDeltaTime);

                var playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (!playerNetObj)
                {
                    Debug.LogError($"[NetworkShipSpawner] Player object not found for client {clientId}");
                    return;
                }

                var player = playerNetObj.GetComponent<Player>();
                if (!player)
                {
                    Debug.LogError($"[NetworkShipSpawner] Player component missing on {clientId}");
                    return;
                }

                // Hook vessel type changes
                player.NetDefaultShipType.OnValueChanged += (oldVal, newVal) =>
                {
                    if (!_netcodeHooks.IsServer) return;
                    if (newVal == VesselClassType.Random) return;

                    Debug.Log($"[NetworkShipSpawner] Client {clientId} vessel type changed {oldVal} → {newVal}");

                    if (player.Vessel is NetworkBehaviour oldVesselNet)
                        oldVesselNet.NetworkObject.Despawn();

                    SpawnVesselForPlayer(clientId, player, preservePosition: oldVal != VesselClassType.Random);
                };

                // Spawn initial vessel if type already chosen
                if (player.NetDefaultShipType.Value != VesselClassType.Random)
                {
                    SpawnVesselForPlayer(clientId, player, preservePosition: false);
                }

                // After spawning, sync for all clients
                await UniTask.Delay(500, DelayType.UnscaledDeltaTime);
                clientPlayerSpawner.InitializeAndSetupPlayer_ClientRpc();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkShipSpawner] Error in DelayedSpawnVesselForPlayer: {ex}");
            }
        }

        // ----------------------------
        // Ensures a late joiner sees all existing Player ↔ Vessel pairs
        // ----------------------------
        private void SyncExistingPlayersForNewClient(ulong newClientId)
        {
            foreach (var clientPair in NetworkManager.Singleton.ConnectedClientsList)
            {
                ulong existingClientId = clientPair.ClientId;
                if (existingClientId == newClientId) continue;

                var playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(existingClientId);
                if (!playerNetObj) continue;

                var player = playerNetObj.GetComponent<Player>();
                if (player == null) continue;

                if (player.Vessel != null)
                {
                    Debug.Log($"[NetworkShipSpawner] Syncing existing vessel for client {existingClientId} → new client {newClientId}");
                    clientPlayerSpawner.InitializeAndSetupPlayer_ClientRpc(new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { newClientId }
                        }
                    });
                }
            }
        }

        // ----------------------------
        // Vessel spawning logic
        // ----------------------------
        private void SpawnVesselForPlayer(ulong clientId, Player networkPlayer, bool preservePosition)
        {
            VesselClassType vesselTypeToSpawn = networkPlayer.NetDefaultShipType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselTypeToSpawn, out Transform shipPrefabTransform))
            {
                Debug.LogError($"[NetworkShipSpawner] No prefab for vessel type {vesselTypeToSpawn}");
                return;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                Debug.LogError($"[NetworkShipSpawner] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return;
            }

            var networkShip = Instantiate(shipNetworkObject);
            networkShip.SpawnWithOwnership(clientId, true);

            // Assign vessel reference
            if (networkShip.TryGetComponent(out IVessel vesselComp))
                networkPlayer.InitializeForMultiplayerMode(vesselComp);

            // Position
            if (preservePosition && networkPlayer.Vessel is Component oldVesselComp)
            {
                networkShip.transform.position = oldVesselComp.transform.position;
                networkShip.transform.rotation = oldVesselComp.transform.rotation;
            }
            else
            {
                Transform spawnPoint = GetRandomSpawnPoint();
                if (spawnPoint != null)
                    networkShip.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            }

            Debug.Log($"[NetworkShipSpawner] Spawned {vesselTypeToSpawn} for client {clientId}");
        }

        // ----------------------------
        // Spawn point picker
        // ----------------------------
        private Transform GetRandomSpawnPoint()
        {
            if (_playerSpawnPoints == null || _playerSpawnPoints.Length == 0)
            {
                Debug.LogError("[NetworkShipSpawner] PlayerSpawnPoints array not set or empty.");
                return null;
            }

            if (_playerSpawnPointsList == null || _playerSpawnPointsList.Count == 0)
                _playerSpawnPointsList = new List<Transform>(_playerSpawnPoints);

            int index = UnityEngine.Random.Range(0, _playerSpawnPointsList.Count);
            Transform spawnPoint = _playerSpawnPointsList[index];
            _playerSpawnPointsList.RemoveAt(index);
            return spawnPoint;
        }
    }
}
