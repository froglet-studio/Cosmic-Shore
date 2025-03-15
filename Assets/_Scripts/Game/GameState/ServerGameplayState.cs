using CosmicShore.NetworkManagement;
using CosmicShore.Utilities;
using CosmicShore.Utilities.Network;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.Samples.Utilities;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using VContainer;

namespace CosmicShore.Game.GameState
{
    [RequireComponent(typeof(NetcodeHooks))]
    public class ServerGameplayState : GameStateBehaviour
    {
        public override GameState ActiveState => GameState.Gameplay;

        [SerializeField]
        private ClientGameplayState _clientGameplayState;

        [SerializeField]
        [Tooltip("A collection of locations for spawning players")]
        private Transform[] _playerSpawnPoints;

        [SerializeField]
        private NetworkShip[] _shipPrefabs;

        /// <summary>
        /// Has the ServerGameplayState already hit its initial spawn? (i.e. spawned players following load from character select).
        /// </summary>
        public bool InitialSpawnDone { get; private set; }

        private NetcodeHooks _netcodeHooks;
        private List<Transform> _playerSpawnPointsList = null;

        [Inject]
        private SceneNameListSO _sceneNameList;

        protected override void Awake()
        {
            base.Awake();
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

            Debug.Log("ServerGameplayState: OnNetworkSpawn invoked on the server.");

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += OnSynchronizeComplete;

            SessionManager<SessionPlayerData>.Instance.OnSessionStarted();
        }

        private void OnNetworkDespawn()
        {
            Debug.Log("ServerGameplayState: OnNetworkDespawn invoked.");
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete -= OnSynchronizeComplete;
        }

        protected override void OnDestroy()
        {
            if (_netcodeHooks != null)
            {
                _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
                _netcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;
            }
            base.OnDestroy();
        }

        void OnSynchronizeComplete(ulong clientId)
        {
            Debug.Log($"OnSynchronizeComplete for client {clientId}.");
            if (InitialSpawnDone && !NetworkShipClientCache.GetShip(clientId))
            {
                Debug.Log($"Late join detected for client {clientId}. Spawning player and ship.");
                SpawnPlayerAndShipForClient(clientId, true);

                // For late joins, wait a bit and then initialize the client gameplay state.
                StartCoroutine(InitializeRoutine());
            }
        }

        IEnumerator InitializeRoutine()
        {
            yield return new WaitForSeconds(3f);
            Debug.Log("InitializeRoutine: Calling InitializeAndSetupPlayer_ClientRpc.");
            _clientGameplayState.InitializeAndSetupPlayer_ClientRpc();
        }

        void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            Debug.Log($"OnLoadEventCompleted: sceneName = {sceneName}, loadSceneMode = {loadSceneMode}");
            if (!InitialSpawnDone && loadSceneMode == LoadSceneMode.Single)
            {
                InitialSpawnDone = true;
                foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
                {
                    Debug.Log($"Spawning player and ship for client {clientPair.Key}.");
                    SpawnPlayerAndShipForClient(clientPair.Key, false);
                }

                Debug.Log("Calling InitializeAndSetupPlayer_ClientRpc for all clients.");
                _clientGameplayState.InitializeAndSetupPlayer_ClientRpc();
            }
        }

        void OnClientDisconnect(ulong clientId)
        {
            Debug.Log($"OnClientDisconnect: client {clientId} disconnected.");
            // If the server itself disconnects (host leaving), load back to character select.
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("Server disconnect detected; loading character select scene.");
                SceneLoaderWrapper.Instance.LoadScene(_sceneNameList.CharSelectScene, useNetworkSceneManager: true);
            }
        }

        void SpawnPlayerAndShipForClient(ulong clientId, bool lateJoin)
        {
            NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerNetworkObject == null)
            {
                Debug.LogError($"SpawnPlayerAndShipForClient: Player NetworkObject not found for client {clientId}.");
                return;
            }

            // Get the ship type from the NetworkPlayer.
            NetworkPlayer networkPlayer = playerNetworkObject.GetComponent<NetworkPlayer>();
            if (networkPlayer == null)
            {
                Debug.LogError($"SpawnPlayerAndShipForClient: NetworkPlayer component not found for client {clientId}.");
                return;
            }
            ShipTypes shipTypeToSpawn = networkPlayer.NetDefaultShipType.Value;

            NetworkObject prefab = GetPrefab(shipTypeToSpawn);
            if (prefab == null)
            {
                Debug.LogError($"SpawnPlayerAndShipForClient: No matching ship prefab found for ship type {shipTypeToSpawn} for client {clientId}.");
                return;
            }

            // Instantiate and spawn the ship.
            NetworkObject networkShip = Instantiate(prefab);
            Assert.IsTrue(networkShip != null, $"Matching ship network object for client {clientId} not found!");
            networkShip.SpawnWithOwnership(clientId, true);
            Debug.Log($"Spawned ship for client {clientId} using ship type {shipTypeToSpawn}.");

            if (lateJoin)
            {
                SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
                if (sessionPlayerData is { HasCharacterSpawned: true })
                {
                    networkShip.transform.SetPositionAndRotation(sessionPlayerData.Value.PlayerPosition, sessionPlayerData.Value.PlayerRotation);
                    Debug.Log($"Late join: Set position for client {clientId} from session data.");
                }
            }
            else
            {
                Transform spawnPoint = GetRandomSpawnPoint();
                if (spawnPoint != null)
                {
                    networkShip.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
                    Debug.Log($"Spawned client {clientId} at random spawn point.");
                }
                else
                {
                    Debug.LogError("No available spawn point found!");
                }
            }
        }

        /// <summary>
        /// Get a random spawn point from the list.
        /// </summary>
        /// <returns>A Transform representing a spawn point.</returns>
        private Transform GetRandomSpawnPoint()
        {
            if (_playerSpawnPoints == null || _playerSpawnPoints.Length == 0)
            {
                Debug.LogError("PlayerSpawnPoints array is not set or empty.");
                return null;
            }

            if (_playerSpawnPointsList == null || _playerSpawnPointsList.Count == 0)
            {
                _playerSpawnPointsList = new List<Transform>(_playerSpawnPoints);
            }

            Debug.Assert(_playerSpawnPointsList.Count > 0, "PlayerSpawnPoints list should have at least 1 spawn point.");

            int index = Random.Range(0, _playerSpawnPointsList.Count);
            Transform spawnPoint = _playerSpawnPointsList[index];
            _playerSpawnPointsList.RemoveAt(index);

            return spawnPoint;
        }

        /// <summary>
        /// Retrieves the ship prefab corresponding to the specified ship type.
        /// </summary>
        /// <param name="shipTypeToSpawn">The ShipTypes to look for.</param>
        /// <returns>The matching NetworkObject prefab or null if not found.</returns>
        NetworkObject GetPrefab(ShipTypes shipTypeToSpawn)
        {
            if (_shipPrefabs == null || _shipPrefabs.Length == 0)
            {
                Debug.LogError("ShipPrefabs array is not set or empty.");
                return null;
            }

            return _shipPrefabs.FirstOrDefault(prefab => prefab.ShipStatus.ShipType == shipTypeToSpawn).GetComponent<NetworkObject>();
        }
    }
}
