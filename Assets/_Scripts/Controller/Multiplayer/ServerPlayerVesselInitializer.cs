using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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
    /// Server-side vessel spawner using OnClientConnectedCallback.
    ///
    /// Flow:
    ///   OnNetworkSpawn → subscribe to OnClientConnectedCallback
    ///   OnClientConnected(clientId) → DelayedSpawnVesselForPlayer(clientId)
    ///     → wait 500ms → get player via GetPlayerNetworkObject → assign domain
    ///     → spawn vessel → notify clients via RPCs
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
        [SerializeField] protected bool shutdownNetworkOnDespawn = true;

        [Header("Spawn Points")]
        [Tooltip("Scene-placed spawn transforms. If set, overrides GameDataSO.SpawnPoses on network spawn.")]
        [SerializeField] protected Transform[] playerSpawnPoints;

        NetcodeHooks _netcodeHooks;
        protected CancellationTokenSource _cts;

        protected virtual void Awake()
        {
            _netcodeHooks = GetComponent<NetcodeHooks>();
            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
            _netcodeHooks.OnNetworkDespawnHook += OnNetworkDespawn;
        }

        protected virtual void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

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

            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                gameData.SetSpawnPositions(playerSpawnPoints);

            _cts = new CancellationTokenSource();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        protected virtual void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (shutdownNetworkOnDespawn && NetworkManager.Singleton)
                NetworkManager.Singleton.Shutdown();
        }

        /// <summary>
        /// Called when a client connects. Override in derived classes to customize behavior.
        /// </summary>
        protected virtual void OnClientConnected(ulong clientId)
        {
            DelayedSpawnVesselForPlayer(clientId).Forget();
        }

        /// <summary>
        /// Spawns a vessel for the given client after a short delay, then notifies all clients.
        /// </summary>
        protected async UniTaskVoid DelayedSpawnVesselForPlayer(ulong clientId)
        {
            try
            {
                await DelayedSpawnVesselForPlayerAsync(clientId);

                await UniTask.Delay(500, DelayType.UnscaledDeltaTime, cancellationToken: _cts.Token);

                NotifyClients(clientId);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Error in DelayedSpawnVesselForPlayer: {ex}");
            }
        }

        async UniTask DelayedSpawnVesselForPlayerAsync(ulong clientId)
        {
            await UniTask.Delay(500, DelayType.UnscaledDeltaTime, cancellationToken: _cts.Token);

            var playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (!playerNetObj)
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Player object not found for client {clientId}");
                return;
            }

            var player = playerNetObj.GetComponent<Player>();
            if (!player)
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Player component missing on {clientId}");
                return;
            }

            player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
            player.NetIsAI.Value = false;

            if (player.NetDefaultVesselType.Value == VesselClassType.Random)
            {
                CSDebug.LogWarning("Vessel type not set, setting default Dolphin");
                player.NetDefaultVesselType.Value = VesselClassType.Dolphin;
            }

            SpawnVesselForPlayer(clientId, player);
        }

        /// <summary>
        /// Sends RPCs to clients after a vessel has been spawned.
        /// Existing clients get just the new player; new client gets all pairs.
        /// </summary>
        protected void NotifyClients(ulong newClientId)
        {
            foreach (var clientPair in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (clientPair.ClientId != newClientId)
                {
                    // Existing clients: initialize just the new joined client's clone
                    var target = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { clientPair.ClientId } }
                    };
                    clientPlayerVesselInitializer.InitializeNewClientsOwnerPlayerAndVesselInExistingClient_ClientRpc(
                        newClientId, target);
                }
                else
                {
                    // New client: initialize ALL existing clones
                    var target = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { newClientId } }
                    };
                    clientPlayerVesselInitializer.InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(target);
                }
            }
        }

        // ---- Vessel Spawning ----

        protected void SpawnVesselForPlayer(ulong clientId, Player networkPlayer)
        {
            VesselClassType vesselTypeToSpawn = networkPlayer.NetDefaultVesselType.Value;
            SpawnVesselForPlayer(clientId, networkPlayer, vesselTypeToSpawn);
        }

        protected NetworkObject SpawnVesselForPlayer(ulong clientId, Player networkPlayer, VesselClassType vesselType)
        {
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

        /// <summary>
        /// Despawns and destroys a vessel's NetworkObject.
        /// </summary>
        protected void DespawnVessel(IVessel vessel)
        {
            gameData.Vessels.Remove(vessel);

            if (vessel is VesselController vc && vc.IsSpawned)
                vc.NetworkObject.Despawn(true);
        }
    }
}
