using System.Collections.Generic;
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
    /// Server-side vessel spawner.
    ///
    /// Flow:
    ///   OnNetworkSpawn → subscribe to OnPlayerNetworkSpawnedUlong
    ///   OnPlayerNetworkSpawnedUlong(ownerClientId) → wait for NetworkVariables to sync
    ///   → spawn vessel → server-side init
    ///   → wait → notify existing clients about new player
    ///   → notify new client about all players
    ///
    /// RPCs:
    ///   New client   → InitializeAllPlayersAndVessels_ClientRpc (all pairs)
    ///   Existing clients → InitializeNewPlayerAndVessel_ClientRpc (just the new pair)
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

        [Header("Timing")]
        [Tooltip("Delay in ms after OnPlayerNetworkSpawned before reading NetworkVariables.")]
        [SerializeField] protected int preSpawnDelayMs = 200;

        [Tooltip("Delay in ms after vessel spawn before notifying clients.")]
        [SerializeField] protected int postSpawnDelayMs = 200;

        NetcodeHooks _netcodeHooks;
        protected CancellationTokenSource _cts;

        /// <summary>
        /// Tracks players already processed (keyed by NetworkObjectId).
        /// Using NetworkObjectId because server-owned AI players share the host's OwnerClientId.
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
                Debug.Log("<color=#00FF00>[FLOW-5] [ServerVesselInit] OnNetworkSpawn — NOT server, disabling</color>");
                enabled = false;
                return;
            }

            Debug.Log($"<color=#00FF00>[FLOW-5] [ServerVesselInit] OnNetworkSpawn — IsServer=true, subscribing to OnPlayerNetworkSpawnedUlong. gameData.Players.Count={gameData.Players.Count}</color>");

            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                gameData.SetSpawnPositions(playerSpawnPoints);

            _cts = new CancellationTokenSource();
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised += HandlePlayerNetworkSpawned;

            // Process players that were already spawned before this initializer
            // existed (e.g. the host's Player object spawned in the Auth scene
            // before Menu_Main loaded). Their SOAP event was already raised and missed.
            ProcessPreExistingPlayers();
        }

        void ProcessPreExistingPlayers()
        {
            // Stage 1: Check gameData.Players (catches players spawned in THIS scene,
            // e.g. AI players whose OnNetworkSpawn() already added them).
            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer && netPlayer.IsSpawned)
                    HandlePlayerNetworkSpawned(netPlayer.OwnerClientId);
            }

            // Stage 2: Trigger spawn chain for persistent human Players.
            // Player NetworkObjects survive Netcode scene loads (DestroyWithScene=false)
            // but are cleared from gameData.Players by ResetRuntimeData().
            // Their OnNetworkSpawn() won't re-fire, so we initiate the spawn chain here.
            // Actual re-initialization (PrepareForNewScene) happens in
            // FindUnprocessedPlayerByOwnerClientId() after the preSpawnDelay,
            // which ensures it runs after any Start()-based list clearing
            // (e.g. scene-placed MultiplayerSetup.DestroyPlayerAndVessel).
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            foreach (var kvp in nm.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                if (playerObj == null || !playerObj.TryGetComponent<Player>(out var player))
                    continue;
                if (!player.IsSpawned || _processedPlayers.Contains(player.NetworkObjectId))
                    continue;

                HandlePlayerNetworkSpawned(player.OwnerClientId);
            }
        }

        protected virtual void OnNetworkDespawn()
        {
            gameData.OnPlayerNetworkSpawnedUlong.OnRaised -= HandlePlayerNetworkSpawned;
            _processedPlayers.Clear();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (shutdownNetworkOnDespawn && NetworkManager.Singleton)
                NetworkManager.Singleton.Shutdown();
        }

        /// <summary>
        /// Called when a Player's OnNetworkSpawn fires. The ownerClientId
        /// identifies which client owns this player. We wait a short delay
        /// for NetworkVariables (NetDomain, NetDefaultVesselType, NetIsAI, NetName)
        /// to replicate, then proceed with vessel spawning.
        /// </summary>
        void HandlePlayerNetworkSpawned(ulong ownerClientId)
        {
            HandlePlayerNetworkSpawnedAsync(ownerClientId, _cts.Token).Forget();
        }

        async UniTaskVoid HandlePlayerNetworkSpawnedAsync(ulong ownerClientId, CancellationToken ct)
        {
            Debug.Log($"<color=#00FF00>[FLOW-5] [ServerVesselInit] HandlePlayerNetworkSpawnedAsync — ownerClientId={ownerClientId}, waiting {preSpawnDelayMs}ms for NetworkVariables</color>");
            // Wait for NetworkVariables set in Player.OnNetworkSpawn to sync
            await UniTask.Delay(preSpawnDelayMs, DelayType.UnscaledDeltaTime, cancellationToken: ct);

            Player player = FindUnprocessedPlayerByOwnerClientId(ownerClientId);
            if (player == null)
            {
                Debug.LogWarning($"<color=#FFA500>[FLOW-5] [ServerVesselInit] FindUnprocessedPlayerByOwnerClientId({ownerClientId}) returned NULL</color>");
                return;
            }

            Debug.Log($"<color=#00FF00>[FLOW-5] [ServerVesselInit] Found player: Name={player.NetName.Value}, VesselType={player.NetDefaultVesselType.Value}, NetworkObjectId={player.NetworkObjectId}</color>");

            // Assign domain if not already set.
            // Persistent players get their domain in PrepareForNewScene() (called by FindUnprocessedPlayerByOwnerClientId).
            // AI players get their domain in SpawnAIPlayerObjects() (already set before reaching here).
            // New human players joining mid-game need assignment now.
            if (player.NetDomain.Value is Domains.Unassigned or Domains.None)
                player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);

            if (!_processedPlayers.Add(player.NetworkObjectId))
            {
                Debug.Log($"<color=#FFA500>[FLOW-5] [ServerVesselInit] Player {player.NetworkObjectId} already processed, skipping</color>");
                return;
            }

            if (!IsReadyToSpawn(player))
            {
                Debug.LogError($"<color=#FF0000>[FLOW-5] [ServerVesselInit] Player {ownerClientId} NOT ready! VesselType={player.NetDefaultVesselType.Value}, Name='{player.NetName.Value}'</color>");
                CSDebug.LogError($"[ServerPlayerVesselInitializer] Player {ownerClientId} not ready after delay. " +
                                 $"VesselType={player.NetDefaultVesselType.Value}, Name={player.NetName.Value}");
                return;
            }

            Debug.Log($"<color=#00FF00>[FLOW-5] [ServerVesselInit] Player ready! Spawning vessel for {player.NetName.Value} (type={player.NetDefaultVesselType.Value})</color>");
            await OnPlayerReadyToSpawnAsync(player, ct);
        }

        /// <summary>
        /// Called when a player's vessel type is confirmed.
        /// Spawns the vessel, initializes on server, waits, then notifies clients via RPCs.
        /// Virtual so derived classes (Menu) can add post-init behavior.
        /// </summary>
        protected virtual async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            Debug.Log($"<color=#00FF00>[FLOW-5] [ServerVesselInit] OnPlayerReadyToSpawnAsync — SpawnVesselAndInitialize for {player.NetName.Value}</color>");
            SpawnVesselAndInitialize(player.OwnerClientId, player);

            Debug.Log($"<color=#00FF00>[FLOW-5] [ServerVesselInit] Vessel spawned. Waiting {postSpawnDelayMs}ms for replication...</color>");
            // Wait for the vessel NetworkObject to fully replicate before telling clients
            await UniTask.Delay(postSpawnDelayMs, DelayType.UnscaledDeltaTime, cancellationToken: ct);

            Debug.Log($"<color=#00FF00>[FLOW-5] [ServerVesselInit] NotifyClients for {player.NetName.Value}</color>");
            NotifyClients(player);
        }

        protected void SpawnVesselAndInitialize(ulong clientId, Player player)
        {
            var vesselNO = SpawnVesselForPlayer(clientId, player);
            if (!vesselNO)
                return;

            if (!vesselNO.TryGetComponent(out IVessel vessel))
            {
                CSDebug.LogError("[ServerPlayerVesselInitializer] Spawned vessel missing IVessel component.");
                return;
            }

            clientPlayerVesselInitializer.InitializePlayerAndVessel(player, vessel);
        }

        /// <summary>
        /// Sends RPCs to non-host clients:
        ///   - Existing clients: "initialize just this new pair"
        ///   - New client: "initialize ALL player-vessel pairs"
        /// </summary>
        protected void NotifyClients(Player newPlayer)
        {
            var newClientId = newPlayer.OwnerClientId;
            var hostClientId = NetworkManager.Singleton.LocalClientId;

            // Tell existing non-host clients about the new player-vessel pair
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId == newClientId) continue;
                if (client.ClientId == hostClientId) continue;

                var existingTarget = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
                };
                clientPlayerVesselInitializer.InitializeNewPlayerAndVessel_ClientRpc(
                    newPlayer.PlayerNetId, newPlayer.VesselNetId, existingTarget);
            }

            // Tell the new client to initialize ALL player-vessel pairs
            if (newClientId != hostClientId)
            {
                var playerIds = new List<ulong>();
                var vesselIds = new List<ulong>();
                foreach (var p in gameData.Players)
                {
                    if (p.VesselNetId == 0) continue;
                    playerIds.Add(p.PlayerNetId);
                    vesselIds.Add(p.VesselNetId);
                }

                var newTarget = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { newClientId } }
                };
                clientPlayerVesselInitializer.InitializeAllPlayersAndVessels_ClientRpc(
                    playerIds.ToArray(), vesselIds.ToArray(), newTarget);
            }
            
            // Invoke Client Ready gameData.InvokeClientReady(); after few interval
        }

        protected NetworkObject SpawnVesselForPlayer(ulong clientId, Player networkPlayer) =>
            SpawnVesselForPlayer(clientId, networkPlayer, networkPlayer.NetDefaultVesselType.Value);

        /// <summary>
        /// Spawns a vessel of the given type, assigns ownership to <paramref name="clientId"/>,
        /// and updates the player's <see cref="Player.NetVesselId"/>.
        /// </summary>
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
        /// Despawns and destroys a vessel's <see cref="NetworkObject"/>.
        /// Removes it from <see cref="GameDataSO.Vessels"/> tracking.
        /// </summary>
        protected void DespawnVessel(IVessel vessel)
        {
            gameData.Vessels.Remove(vessel);

            if (vessel is VesselController vc && vc.IsSpawned)
                vc.NetworkObject.Despawn(true);
        }

        /// <summary>
        /// Finds the first unprocessed Player owned by the given clientId.
        /// Falls back to NetworkManager.ConnectedClients for persistent Players
        /// that may have been cleared from gameData.Players during scene transition
        /// (by ResetRuntimeData or DestroyPlayerAndVessel). If found via fallback,
        /// calls PrepareForNewScene() to re-initialize for the current game config.
        /// </summary>
        Player FindUnprocessedPlayerByOwnerClientId(ulong ownerClientId)
        {
            foreach (var p in gameData.Players)
            {
                if (p is Player netPlayer
                    && netPlayer.IsSpawned
                    && netPlayer.OwnerClientId == ownerClientId
                    && !_processedPlayers.Contains(netPlayer.NetworkObjectId))
                {
                    return netPlayer;
                }
            }

            // Fallback: discover persistent Player from ConnectedClients.
            // Player may have been cleared from gameData.Players after
            // ProcessPreExistingPlayers() triggered the spawn chain
            // (e.g. scene-placed MultiplayerSetup.Start() → DestroyPlayerAndVessel).
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;

            if (!nm.ConnectedClients.TryGetValue(ownerClientId, out var client))
                return null;

            var playerObj = client.PlayerObject;
            if (playerObj == null || !playerObj.TryGetComponent<Player>(out var player))
                return null;

            if (!player.IsSpawned || _processedPlayers.Contains(player.NetworkObjectId))
                return null;

            // Re-initialize the persistent Player for the current game scene.
            player.PrepareForNewScene();
            return player;
        }

        /// <summary>
        /// A player is ready to spawn when both vessel type and name are set.
        /// </summary>
        protected bool IsReadyToSpawn(Player player) =>
            IsValidVesselType(player.NetDefaultVesselType.Value)
            && !string.IsNullOrEmpty(player.NetName.Value.ToString());

        protected static bool IsValidVesselType(VesselClassType type) =>
            type != VesselClassType.Random && type != VesselClassType.Any;
    }
}
