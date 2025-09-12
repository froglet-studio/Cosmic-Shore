﻿using CosmicShore.Utility.ClassExtensions;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Soap;
using Unity.Multiplayer.Samples.Utilities;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;


namespace CosmicShore.Game
{
    /// <summary>
    /// Player prefab is spawned automatically in network, hence we are
    /// spawning the network vessels and hooking it with network players.
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    public class NetworkShipSpawner : MonoBehaviour
    {
        [FormerlySerializedAs("_clientGameplayState")] [SerializeField]
        ClientPlayerSpawner clientPlayerSpawner;

        [SerializeField]
        [Tooltip("A collection of locations for spawning players")]
        Transform[] _playerSpawnPoints;

        [SerializeField] 
        ShipPrefabContainer shipPrefabContainer;

        [SerializeField]
        string _mainMenuSceneName = "Menu_Main";

        /// <summary>
        /// Has the ServerGameplayState already hit its initial spawn? (i.e. spawned players following load from character select).
        /// </summary>
        public bool InitialSpawnDone { get; private set; }

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

            Debug.Log("ServerGameplayState: OnNetworkSpawn invoked on the server.");

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += OnSynchronizeComplete;
        }

        private void OnNetworkDespawn()
        {
            Debug.Log("ServerGameplayState: OnNetworkDespawn invoked.");

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnSynchronizeComplete -= OnSynchronizeComplete;
        }

        private void OnDestroy()
        {
            if (_netcodeHooks != null)
            {
                _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
                _netcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;
            }
        }

        async void OnSynchronizeComplete(ulong clientId)
        {
            Debug.Log($"OnSynchronizeComplete for client {clientId}.");

            await UniTask.Delay(3000);

            if (InitialSpawnDone && !NetworkShipClientCache.GetInstanceByClientId(clientId))
            {
                Debug.Log($"Late join detected for client {clientId}. Spawning player and ship.");
                SpawnShipForClient(clientId, true);

                await UniTask.Delay(3000);

                // For late joins, wait a bit and then initialize the client gameplay state.
                clientPlayerSpawner.InitializeAndSetupPlayer_ClientRpc();

                if (MultiplayerSetup.Instance.ActiveSession.AvailableSlots == 0)
                {
                    DebugExtensions.LogColored("All players have joined; starting the game.", Color.green);
                }
            }
        }

        void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            Debug.Log($"OnLoadEventCompleted: sceneName = {sceneName}, loadSceneMode = {loadSceneMode}");
            if (InitialSpawnDone || loadSceneMode != LoadSceneMode.Single) 
                return;
            
            InitialSpawnDone = true;
            foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
            {
                Debug.Log($"Spawning player and ship for client {clientPair.Key}.");
                SpawnShipForClient(clientPair.Key, false);
            }

            Debug.Log("Calling InitializeAndSetupPlayer_ClientRpc for all clients.");
            clientPlayerSpawner.InitializeAndSetupPlayer_ClientRpc();
        }

        void OnClientDisconnect(ulong clientId)
        {
            // if it is server, then tell MultiplayerSertup to free the team of this player

            Debug.Log($"OnClientDisconnect: client {clientId} disconnected.");
            // If the server itself disconnects (host leaving), load back to character select.
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("Server disconnect detected; loading character select scene.");
                SceneManager.LoadSceneAsync(_mainMenuSceneName, LoadSceneMode.Single);
            }
        }

        void SpawnShipForClient(ulong clientId, bool lateJoin)
        {
            NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerNetworkObject == null)
            {
                Debug.LogError($"SpawnPlayerAndShipForClient: Player NetworkObject not found for client {clientId}.");
                return;
            }

            // Get the ship type from the NetworkPlayer.
            R_Player networkPlayer = playerNetworkObject.GetComponent<R_Player>();
            if (networkPlayer == null)
            {
                Debug.LogError($"SpawnPlayerAndShipForClient: NetworkPlayer component not found for client {clientId}.");
                return;
            }

            // Teams team = networkPlayer.NetTeam.Value;
            ShipClassType shipTypeToSpawn = networkPlayer.NetDefaultShipType.Value;

            if (!shipPrefabContainer.TryGetShipPrefab(shipTypeToSpawn, out Transform shipPrefabTransform))
                return;

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                Debug.LogError($"SpawnPlayerAndShipForClient: No matching ship prefab found for ship type {shipTypeToSpawn} for client {clientId}.");
                return;
            }

            // Instantiate and spawn the ship.
            NetworkObject networkShip = Instantiate(shipNetworkObject);
            Assert.IsTrue(networkShip != null, $"Matching ship network object for client {clientId} not found!");
            networkShip.SpawnWithOwnership(clientId, true);
            Debug.Log($"Spawned ship for client {clientId} using ship type {shipTypeToSpawn}.");

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
    }
}
