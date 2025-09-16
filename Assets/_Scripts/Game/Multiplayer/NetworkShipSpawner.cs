using System;
using System.Collections.Generic;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Multiplayer.Samples.Utilities;
using CosmicShore.Utility.ClassExtensions;
using Obvious.Soap;

namespace CosmicShore.Game
{
    /// <summary>
    /// Server-side system: Spawns networked vessels and keeps them synced with player prefab state.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    public class NetworkShipSpawner : MonoBehaviour
    {
        [SerializeField] ScriptableEventNoParam OnAllClientJoined;
        [SerializeField] ClientPlayerSpawner clientPlayerSpawner;
        [SerializeField, Tooltip("A collection of locations for spawning players")]
        Transform[] _playerSpawnPoints;
        [SerializeField] ShipPrefabContainer shipPrefabContainer;
        [SerializeField] string _mainMenuSceneName = "Menu_Main";

        bool initialSpawnDone;
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

            Debug.Log("[NetworkShipSpawner] OnNetworkSpawn invoked on the server.");

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += OnSynchronizeComplete;

            NetworkVesselClientCache.OnNewInstanceAdded += OnNewVesselClientAdded;
        }

        private void OnNetworkDespawn()
        {
            Debug.Log("[NetworkShipSpawner] OnNetworkDespawn invoked.");

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete -= OnSynchronizeComplete;

            NetworkVesselClientCache.OnNewInstanceAdded -= OnNewVesselClientAdded;
        }

        private void OnDestroy()
        {
            if (_netcodeHooks != null)
            {
                _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
                _netcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;
            }
        }

        void OnNewVesselClientAdded(IVessel _)
        {
            if (MultiplayerSetup.Instance.ActiveSession.AvailableSlots == 0)
            {
                DebugExtensions.LogColored("[NetworkShipSpawner] All players have joined; starting the game.", Color.green);
                OnAllClientJoined?.Raise();
            }
        }

        void OnSynchronizeComplete(ulong clientId)
        {
            Debug.Log($"[NetworkShipSpawner] OnSynchronizeComplete for client {clientId}.");

            if (initialSpawnDone && !NetworkVesselClientCache.GetInstanceByClientId(clientId))
            {
                Debug.Log($"[NetworkShipSpawner] Late join detected for client {clientId}. Spawning player and vessel.");
                ExecutePlayerConfigAndVesselSpawn(clientId, true);
                DelayedInitializeClientAsync().Forget();
            }
        }

        void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            Debug.Log($"[NetworkShipSpawner] OnLoadEventCompleted: {sceneName}, mode={loadSceneMode}");
            if (initialSpawnDone || loadSceneMode != LoadSceneMode.Single)
                return;

            initialSpawnDone = true;
            foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
            {
                ExecutePlayerConfigAndVesselSpawn(clientPair.Key, false);
            }

            Debug.Log("[NetworkShipSpawner] Initial spawn done. Initializing players after short delay.");
            DelayedInitializeClientAsync().Forget();
        }

        void OnClientDisconnect(ulong clientId)
        {
            Debug.Log($"[NetworkShipSpawner] OnClientDisconnect: client {clientId} disconnected.");

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[NetworkShipSpawner] Host disconnected; returning to main menu.");
                SceneManager.LoadSceneAsync(_mainMenuSceneName, LoadSceneMode.Single);
            }
        }

        void ExecutePlayerConfigAndVesselSpawn(ulong clientId, bool lateJoin)
        {
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

            // Hook vessel type changes for this player
            player.NetDefaultShipType.OnValueChanged += (oldVal, newVal) =>
            {
                if (!_netcodeHooks.IsServer) return;
                if (newVal == VesselClassType.Random) return;

                Debug.Log($"[NetworkShipSpawner] Client {clientId} vessel type changed {oldVal} → {newVal}");

                // despawn old vessel if exists
                if (player.Vessel is NetworkBehaviour oldVesselNet)
                {
                    oldVesselNet.NetworkObject.Despawn();
                }

                // spawn new vessel
                SpawnVesselForPlayer(clientId, player, preservePosition: oldVal != VesselClassType.Random);
            };

            // Spawn immediately if type already chosen
            if (player.NetDefaultShipType.Value != VesselClassType.Random)
            {
                SpawnVesselForPlayer(clientId, player, preservePosition: false);
            }
        }

        void SpawnVesselForPlayer(ulong clientId, Player networkPlayer, bool preservePosition)
        {
            VesselClassType vesselTypeToSpawn = networkPlayer.NetDefaultShipType.Value;

            if (!shipPrefabContainer.TryGetShipPrefab(vesselTypeToSpawn, out Transform shipPrefabTransform))
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

            // assign vessel reference to player
            if (networkShip.TryGetComponent(out IVessel vesselComp))
            {
                networkPlayer.InitializeForMultiplayerMode(vesselComp);
            }

            // set position
            if (preservePosition && networkPlayer.Vessel is Component oldVesselComp)
            {
                networkShip.transform.position = oldVesselComp.transform.position;
                networkShip.transform.rotation = oldVesselComp.transform.rotation;
            }
            else
            {
                Transform spawnPoint = GetRandomSpawnPoint();
                if (spawnPoint != null)
                {
                    networkShip.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
                }
            }

            Debug.Log($"[NetworkShipSpawner] Spawned {vesselTypeToSpawn} for client {clientId}");
        }

        async UniTask DelayedInitializeClientAsync()
        {
            try
            {
                await UniTask.Delay(2000, DelayType.UnscaledDeltaTime);
                Debug.Log("[NetworkShipSpawner] Running InitializeAndSetupPlayer_ClientRpc after delay.");
                clientPlayerSpawner.InitializeAndSetupPlayer_ClientRpc();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkShipSpawner] Error in delayed init: {ex}");
            }
        }

        private Transform GetRandomSpawnPoint()
        {
            if (_playerSpawnPoints == null || _playerSpawnPoints.Length == 0)
            {
                Debug.LogError("[NetworkShipSpawner] PlayerSpawnPoints array not set or empty.");
                return null;
            }

            if (_playerSpawnPointsList == null || _playerSpawnPointsList.Count == 0)
            {
                _playerSpawnPointsList = new List<Transform>(_playerSpawnPoints);
            }

            int index = UnityEngine.Random.Range(0, _playerSpawnPointsList.Count);
            Transform spawnPoint = _playerSpawnPointsList[index];
            _playerSpawnPointsList.RemoveAt(index);
            return spawnPoint;
        }
    }
}
