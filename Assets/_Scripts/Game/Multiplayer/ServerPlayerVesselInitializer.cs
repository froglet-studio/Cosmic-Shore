using System;
using System.Collections.Generic;
using CosmicShore.Game.AI;
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
    /// - In solo mode (IsMultiplayerMode == false), spawns AI opponents after the host player connects
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

        [Header("AI Ship Selection")]
        [Tooltip("Game list used to look up available ships for AI opponents. If unset, AI defaults to Sparrow.")]
        [SerializeField] SO_GameList gameList;

        [Header("Spawn Origins")]
        [SerializeField] protected Transform[] _playerOrigins;

        protected NetcodeHooks _netcodeHooks;

        public Action OnAllPlayersSpawned;

        bool IsSoloWithAI => !gameData.IsMultiplayerMode;

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
            if (IsSoloWithAI && clientId == NetworkManager.Singleton.LocalClientId)
            {
                SpawnPlayerThenAI(clientId).Forget();
            }
            else
            {
                DelayedSpawnVesselForPlayer(clientId).Forget();
            }
        }

        // ----------------------------
        // Solo mode: spawn the human player's vessel, then spawn AI opponents
        // ----------------------------
        async UniTaskVoid SpawnPlayerThenAI(ulong clientId)
        {
            // First, spawn the human player's vessel normally
            await DelayedSpawnVesselForPlayerAsync(clientId);

            // Wait for human player initialization to complete
            await UniTask.WaitForSeconds(1f, true);

            // Spawn AI opponents
            SpawnAIOpponents();

            // Allow a moment for AI network objects to propagate, then initialize all
            await UniTask.WaitForSeconds(0.5f, true);

            var target = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            };
            clientPlayerVesselInitializer.InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(
                new ClientRpcParams { Send = target }
            );
        }

        void SpawnAIOpponents()
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
                return;

            // Get the player prefab from NetworkManager to use for AI
            var playerPrefabGO = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            if (!playerPrefabGO)
            {
                Debug.LogError("[ServerPlayerVesselInitializer] No player prefab configured in NetworkManager.");
                return;
            }

            var playerPrefabNO = playerPrefabGO.GetComponent<NetworkObject>();
            if (!playerPrefabNO)
            {
                Debug.LogError("[ServerPlayerVesselInitializer] Player prefab missing NetworkObject component.");
                return;
            }

            // Determine how many AI opponents to spawn (fill remaining slots up to MaxPlayers)
            int aiCount = 1;
            var game = FindGameByMode(gameData.GameMode);
            if (game != null && game.MaxPlayers > 1)
                aiCount = game.MaxPlayers - 1;

            for (int i = 0; i < aiCount; i++)
            {
                var aiPlayerNO = Instantiate(playerPrefabNO);

                // Position AI at successive spawn points (index 1, 2, ...)
                int spawnIndex = 1 + i;
                if (_playerOrigins != null && spawnIndex < _playerOrigins.Length)
                    aiPlayerNO.transform.SetPositionAndRotation(_playerOrigins[spawnIndex].position, _playerOrigins[spawnIndex].rotation);

                aiPlayerNO.Spawn(true); // server-owned

                var aiPlayer = aiPlayerNO.GetComponent<Player>();
                if (!aiPlayer)
                {
                    Debug.LogError("[ServerPlayerVesselInitializer] AI Player prefab missing Player component.");
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                // Configure AI player
                var aiDomain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
                var aiVesselType = PickAIVesselType();

                aiPlayer.NetDefaultVesselType.Value = aiVesselType;
                aiPlayer.NetName.Value = $"AI Pilot {i + 1}";
                aiPlayer.NetDomain.Value = aiDomain;
                aiPlayer.NetIsAI.Value = true;

                // Spawn AI vessel (server-owned)
                if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
                {
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                // Configure the AI pilot on the spawned vessel
                ConfigureAIPilot(aiVesselNO);

                Debug.Log($"[ServerPlayerVesselInitializer] Spawned AI opponent {i + 1}/{aiCount}: domain={aiDomain}, vessel={aiVesselType}");
            }
        }

        protected VesselClassType PickAIVesselType()
        {
            // Try to pick a random ship from the game's available captains
            if (gameList != null)
            {
                var game = FindGameByMode(gameData.GameMode);
                if (game != null && game.Captains != null && game.Captains.Count > 0)
                {
                    var captain = game.Captains[UnityEngine.Random.Range(0, game.Captains.Count)];
                    if (captain != null && captain.Ship != null)
                    {
                        var shipType = captain.Ship.Class;
                        // Validate the prefab container can spawn this type
                        if (vesselPrefabContainer.TryGetShipPrefab(shipType, out _))
                        {
                            Debug.Log($"[ServerPlayerVesselInitializer] AI picking ship {shipType} from captain {captain.Name}");
                            return shipType;
                        }
                        Debug.LogWarning($"[ServerPlayerVesselInitializer] No prefab for {shipType}, falling back to Sparrow");
                    }
                }
            }

            return VesselClassType.Sparrow;
        }

        protected SO_ArcadeGame FindGameByMode(GameModes mode)
        {
            if (gameList == null || gameList.Games == null)
                return null;

            foreach (var game in gameList.Games)
            {
                if (game.Mode == mode)
                    return game;
            }
            return null;
        }

        protected void ConfigureAIPilot(NetworkObject aiVesselNO)
        {
            var aiPilot = aiVesselNO.GetComponentInChildren<AIPilot>();
            if (aiPilot == null)
                return;

            // Determine if this game mode needs player-seeking behavior (Joust)
            bool shouldSeekPlayers = gameData.GameMode == GameModes.MultiplayerJoust;

            // Scale AI skill with intensity (0.25 per intensity level, capped at 1)
            float skill = Mathf.Clamp01(gameData.SelectedIntensity.Value * 0.25f);

            aiPilot.ConfigureForGameMode(gameData, shouldSeekPlayers, skill);
        }

        bool TrySpawnVesselForAI(Player aiPlayer, out NetworkObject vesselNO)
        {
            vesselNO = null;
            var vesselType = aiPlayer.NetDefaultVesselType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefabTransform))
            {
                Debug.LogError($"[ServerPlayerVesselInitializer] No prefab for AI vessel type {vesselType}");
                return false;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                Debug.LogError($"[ServerPlayerVesselInitializer] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return false;
            }

            vesselNO = Instantiate(shipNetworkObject);
            vesselNO.transform.SetPositionAndRotation(aiPlayer.transform.position, aiPlayer.transform.rotation);
            vesselNO.Spawn(true); // server-owned
            aiPlayer.NetVesselId.Value = vesselNO.NetworkObjectId;
            return true;
        }

        // ----------------------------
        // Spawn vessel for a new client after short delay
        // ----------------------------
        protected async UniTaskVoid DelayedSpawnVesselForPlayer(ulong clientId)
        {
            try
            {
                await DelayedSpawnVesselForPlayerAsync(clientId);

                await UniTask.Delay(500, DelayType.UnscaledDeltaTime);

                foreach (var clientPair in NetworkManager.Singleton.ConnectedClientsList)
                {
                    // Existing clients: initialize just the NEW joined client's clone (Player) in the existing clients.
                    if (clientPair.ClientId != clientId)
                    {
                        var target = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { clientPair.ClientId }
                        };

                        clientPlayerVesselInitializer.InitializeNewClientsOwnerPlayerAndVesselInExistingClient_ClientRpc(
                            clientId,
                            new ClientRpcParams { Send = target }
                        );
                    }
                    // New client: initialize ALL existing clones (Players and AIs) in the new client.
                    else
                    {
                        var target = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { clientId }
                        };

                        clientPlayerVesselInitializer.InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(
                            new ClientRpcParams { Send = target }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerPlayerVesselInitializer] Error in DelayedSpawnVesselForPlayer: {ex}");
            }
        }

        async UniTask DelayedSpawnVesselForPlayerAsync(ulong clientId)
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

            player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
            player.NetIsAI.Value = false;

            // Spawn initial vessel if type already chosen
            if (player.NetDefaultVesselType.Value == VesselClassType.Random)
            {
                Debug.LogWarning("Vessel type not set, setting default dolphin");
                player.NetDefaultVesselType.Value = VesselClassType.Dolphin;
            }
            SpawnVesselForPlayer(clientId, player);
        }

        // ----------------------------
        // Vessel spawning logic
        // ----------------------------
        void SpawnVesselForPlayer(ulong clientId, Player networkPlayer)
        {
            VesselClassType vesselTypeToSpawn = networkPlayer.NetDefaultVesselType.Value;

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

            var networkVessel = Instantiate(shipNetworkObject);
            networkVessel.SpawnWithOwnership(clientId, true);
            networkPlayer.NetVesselId.Value = networkVessel.NetworkObjectId;
        }
    }
}
