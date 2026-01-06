using System;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using Unity.Multiplayer.Samples.Utilities;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    /// <summary>
    /// Server-side system:
    /// - Spawns networked vessels for connecting clients
    /// - Keeps late-join sync via client RPCs (new client gets all clones; existing clients get just the new clone)
    ///
    /// Designed as an inheritance-friendly base class (protected virtual hooks).
    /// </summary>
    [RequireComponent(typeof(NetcodeHooks))]
    public class ServerPlayerVesselInitializer : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] protected GameDataSO gameData;

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

        /// <summary>
        /// Called when the NetworkObject lifecycle spawns (hooked via NetcodeHooks).
        /// Override allowed; call base.OnNetworkSpawn().
        /// </summary>
        protected virtual void OnNetworkSpawn()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                enabled = false;
                return;
            }

            gameData.SetSpawnPositions(_playerOrigins);

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkVesselClientCache.OnNewInstanceAdded += OnNewVesselClientAdded;

            OnServerReady();
        }

        /// <summary>
        /// Called when the NetworkObject lifecycle despawns (hooked via NetcodeHooks).
        /// Override allowed; call base.OnNetworkDespawn().
        /// </summary>
        protected virtual void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            NetworkVesselClientCache.OnNewInstanceAdded -= OnNewVesselClientAdded;

            if (NetworkManager.Singleton)
                NetworkManager.Singleton.Shutdown();
        }

        // ----------------------------
        // Hook points for derived classes
        // ----------------------------

        /// <summary>
        /// Server is ready (after subscriptions + spawn positions). Good place to spawn server-owned entities (AI).
        /// </summary>
        protected virtual void OnServerReady() { }

        /// <summary>
        /// Called after the new client receives "initialize all players/vessels" RPC.
        /// Derived classes can push additional late-join sync here (e.g., AI).
        /// </summary>
        protected virtual void OnAfterInitializeAllPlayersInNewClient(ulong newClientId) { }

        // ----------------------------
        // Lobby full check (human vessels)
        // ----------------------------
        protected virtual void OnNewVesselClientAdded(IVessel _)
        {
            var session = gameData.ActiveSession;
            if (session is { AvailableSlots: 0 })
            {
                OnAllPlayersSpawned?.Invoke();
            }
        }

        // ----------------------------
        // New client connected
        // ----------------------------
        protected virtual void OnClientConnected(ulong clientId)
        {
            SpawnVesselAndInitializeWithPlayer(clientId);
        }

        protected virtual void SpawnVesselAndInitializeWithPlayer(ulong clientId)
        {
            Debug.Log($"[ServerPlayerVesselInitializer] Client {clientId} connected → syncing vessels.");
            DelayedSpawnVesselForPlayer(clientId).Forget();
        }

        // ----------------------------
        // Spawn vessel for a new client after short delay
        // ----------------------------
        protected virtual async UniTaskVoid DelayedSpawnVesselForPlayer(ulong clientId)
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
                    // Existing clients: initialize just the NEW client's clone
                    if (clientPair.ClientId != clientId)
                    {
                        var target = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { clientPair.ClientId }
                        };

                        clientPlayerVesselInitializer.InitializeNewPlayerAndVesselInThisClient_ClientRpc(
                            clientId,
                            new ClientRpcParams { Send = target }
                        );
                    }
                    // New client: initialize ALL existing clones
                    else
                    {
                        var target = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { clientId }
                        };

                        clientPlayerVesselInitializer.InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(
                            new ClientRpcParams { Send = target }
                        );

                        // Derived classes can push extra late-join sync here (AI, etc.)
                        OnAfterInitializeAllPlayersInNewClient(clientId);
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
        protected virtual void SpawnVesselForPlayer(ulong clientId, Player networkPlayer)
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
        }
    }
}
