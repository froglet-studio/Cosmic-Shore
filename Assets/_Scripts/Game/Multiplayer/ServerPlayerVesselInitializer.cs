using System;
using System.Collections.Generic;
using CosmicShore.Game.AI;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using Unity.Multiplayer.Samples.Utilities;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Utility;

namespace CosmicShore.Game
{
    /// <summary>
    /// Server-side system:
    /// - Spawns networked vessels for connecting clients
    /// - Keeps late-join sync via client RPCs
    /// - In solo mode, spawns AI opponents after the host player connects
    ///
    /// PARTY MODE has two states:
    /// - SpawnMode: First round — spawns vessels + AI when triggered by party controller.
    ///   Does NOT subscribe to OnClientConnected. Does NOT shutdown on despawn.
    /// - InertMode: Subsequent rounds — fully inert. Vessels already exist.
    ///   Does NOT shutdown on despawn.
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

        [Header("AI Profiles")]
        [Tooltip("Optional AI profile list for assigning unique names to AI opponents.")]
        [SerializeField] SO_AIProfileList aiProfileList;

        [Header("Spawn Origins")]
        [SerializeField] protected Transform[] _playerOrigins;

        /// <summary>
        /// Read-only access to spawn origin transforms. Used by PartyGameController
        /// to update spawn positions when switching mini-game environments.
        /// </summary>
        public Transform[] PlayerOrigins => _playerOrigins;

        protected NetcodeHooks _netcodeHooks;

        public Action OnAllPlayersSpawned;

        public enum PartyModeState
        {
            /// <summary>Not in party mode — normal standalone behavior.</summary>
            Off,
            /// <summary>Party mode first round — spawns vessels when triggered by party controller.</summary>
            SpawnMode,
            /// <summary>Party mode subsequent rounds — fully inert, vessels already exist.</summary>
            InertMode
        }

        /// <summary>
        /// Controls how this SPVI behaves in party mode.
        /// Set by PartyGameController before the environment is activated.
        /// </summary>
        public PartyModeState PartyMode { get; set; } = PartyModeState.Off;

        /// <summary>Convenience check.</summary>
        public bool IsInPartyMode => PartyMode != PartyModeState.Off;

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

        protected virtual void OnNetworkSpawn()
        {
            // Party InertMode: skip everything — vessels already exist from a previous round
            if (PartyMode == PartyModeState.InertMode)
            {
                CSDebug.Log($"[SPVI] OnNetworkSpawn — PARTY INERT on '{gameObject.name}', skipping all.");
                enabled = false;
                return;
            }

            // Party SpawnMode: set spawn positions but DON'T subscribe to OnClientConnected.
            // The party controller will call SpawnVesselsForParty() explicitly.
            if (PartyMode == PartyModeState.SpawnMode)
            {
                if (!NetworkManager.Singleton.IsServer)
                {
                    enabled = false;
                    return;
                }

                if (_playerOrigins is not { Length: > 0 } || _playerOrigins[0] == null)
                {
                    CSDebug.LogWarning($"[SPVI] PARTY SPAWN on '{gameObject.name}' but no _playerOrigins!");
                    enabled = false;
                    return;
                }

                gameData.SetSpawnPositions(_playerOrigins);
                CSDebug.Log($"[SPVI] OnNetworkSpawn — PARTY SPAWN on '{gameObject.name}', positions set. Waiting for trigger.");
                return;
            }

            // Normal standalone mode
            if (!NetworkManager.Singleton.IsServer)
            {
                enabled = false;
                return;
            }

            if (_playerOrigins is not { Length: > 0 } || _playerOrigins[0] == null)
            {
                CSDebug.LogWarning($"[SPVI] _playerOrigins not assigned on '{gameObject.name}', skipping initialization.");
                enabled = false;
                return;
            }

            gameData.SetSpawnPositions(_playerOrigins);

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkVesselClientCache.OnNewInstanceAdded += OnNewVesselClientAdded;
        }

        protected virtual void OnNetworkDespawn()
        {
            // CRITICAL: In party mode (any state), do NOT shut down NetworkManager.
            if (IsInPartyMode)
            {
                CSDebug.Log($"[SPVI] OnNetworkDespawn — PARTY MODE on '{gameObject.name}', skipping shutdown.");
                return;
            }

            if (NetworkManager.Singleton)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            NetworkVesselClientCache.OnNewInstanceAdded -= OnNewVesselClientAdded;

            if (NetworkManager.Singleton)
                NetworkManager.Singleton.Shutdown();
        }

        // ==================== Party Mode API ====================

        /// <summary>
        /// Called by PartyGameController on the first round to spawn vessels + AI.
        /// Only runs when PartyMode == SpawnMode.
        /// </summary>
        public void SpawnVesselsForParty()
        {
            if (PartyMode != PartyModeState.SpawnMode) return;
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;

            CSDebug.Log($"[SPVI] SpawnVesselsForParty on '{gameObject.name}'");

            ulong hostClientId = NetworkManager.Singleton.LocalClientId;
            SpawnPlayerThenAIForParty(hostClientId).Forget();
        }

        async UniTaskVoid SpawnPlayerThenAIForParty(ulong clientId)
        {
            await DelayedSpawnVesselForPlayerAsync(clientId);
            await UniTask.WaitForSeconds(1f, true);

            SpawnAIOpponents();

            await UniTask.WaitForSeconds(0.5f, true);

            var target = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            };
            clientPlayerVesselInitializer.InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(
                new ClientRpcParams { Send = target }
            );

            OnAllPlayersSpawned?.Invoke();
            CSDebug.Log($"[SPVI] Party vessel spawning complete on '{gameObject.name}'");
        }

        // ==================== Standard Hooks ====================

        protected virtual void OnNewVesselClientAdded(IVessel _)
        {
            var session = gameData.ActiveSession;
            if (session is { AvailableSlots: 0 })
            {
                OnAllPlayersSpawned?.Invoke();
            }
        }

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

        async UniTaskVoid SpawnPlayerThenAI(ulong clientId)
        {
            await DelayedSpawnVesselForPlayerAsync(clientId);
            await UniTask.WaitForSeconds(1f, true);

            SpawnAIOpponents();

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

            var playerPrefabGO = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            if (!playerPrefabGO)
            {
                CSDebug.LogError("[SPVI] No player prefab configured in NetworkManager.");
                return;
            }

            var playerPrefabNO = playerPrefabGO.GetComponent<NetworkObject>();
            if (!playerPrefabNO)
            {
                CSDebug.LogError("[SPVI] Player prefab missing NetworkObject component.");
                return;
            }

            int aiCount = 1;
            var game = FindGameByMode(gameData.GameMode);
            if (game != null && game.MaxPlayers > 1)
                aiCount = game.MaxPlayers - 1;

            var pickedProfiles = aiProfileList != null ? aiProfileList.PickRandom(aiCount) : null;

            for (int i = 0; i < aiCount; i++)
            {
                var aiPlayerNO = Instantiate(playerPrefabNO);

                if (_playerOrigins != null && _playerOrigins.Length > 0)
                {
                    int spawnIndex = 1 + i;
                    if (spawnIndex >= _playerOrigins.Length)
                    {
                        CSDebug.LogWarning($"[SPVI] Not enough spawn origins for AI {i}, wrapping with modulo.");
                        spawnIndex = spawnIndex % _playerOrigins.Length;
                    }
                    aiPlayerNO.transform.SetPositionAndRotation(_playerOrigins[spawnIndex].position, _playerOrigins[spawnIndex].rotation);
                }

                aiPlayerNO.Spawn(true);

                var aiPlayer = aiPlayerNO.GetComponent<Player>();
                if (!aiPlayer)
                {
                    CSDebug.LogError("[SPVI] AI Player prefab missing Player component.");
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                var aiDomain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
                var aiVesselType = PickAIVesselType();

                string aiName = (pickedProfiles != null && i < pickedProfiles.Count && !string.IsNullOrEmpty(pickedProfiles[i].Name))
                    ? pickedProfiles[i].Name
                    : $"AI Pilot {i + 1}";

                aiPlayer.NetDefaultVesselType.Value = aiVesselType;
                aiPlayer.NetName.Value = aiName;
                aiPlayer.NetDomain.Value = aiDomain;
                aiPlayer.NetIsAI.Value = true;

                if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
                {
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                ConfigureAIPilot(aiVesselNO);
                CSDebug.Log($"[SPVI] Spawned AI opponent {i + 1}/{aiCount}: domain={aiDomain}, vessel={aiVesselType}");
            }
        }

        protected VesselClassType PickAIVesselType()
        {
            if (gameList != null)
            {
                var game = FindGameByMode(gameData.GameMode);
                if (game != null && game.Captains != null && game.Captains.Count > 0)
                {
                    var captain = game.Captains[UnityEngine.Random.Range(0, game.Captains.Count)];
                    if (captain != null && captain.Ship != null)
                    {
                        var shipType = captain.Ship.Class;
                        if (vesselPrefabContainer.TryGetShipPrefab(shipType, out _))
                        {
                            CSDebug.Log($"[SPVI] AI picking ship {shipType} from captain {captain.Name}");
                            return shipType;
                        }
                        CSDebug.LogWarning($"[SPVI] No prefab for {shipType}, falling back to Sparrow");
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
            if (aiPilot == null) return;

            bool shouldSeekPlayers = gameData.GameMode == GameModes.MultiplayerJoust;
            float skill = Mathf.Clamp01(gameData.SelectedIntensity.Value * 0.25f);
            aiPilot.ConfigureForGameMode(gameData, shouldSeekPlayers, skill);
        }

        bool TrySpawnVesselForAI(Player aiPlayer, out NetworkObject vesselNO)
        {
            vesselNO = null;
            var vesselType = aiPlayer.NetDefaultVesselType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefabTransform))
            {
                CSDebug.LogError($"[SPVI] No prefab for AI vessel type {vesselType}");
                return false;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError($"[SPVI] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return false;
            }

            vesselNO = Instantiate(shipNetworkObject);
            vesselNO.transform.SetPositionAndRotation(aiPlayer.transform.position, aiPlayer.transform.rotation);
            vesselNO.Spawn(true);
            aiPlayer.NetVesselId.Value = vesselNO.NetworkObjectId;
            return true;
        }

        protected async UniTaskVoid DelayedSpawnVesselForPlayer(ulong clientId)
        {
            try
            {
                await DelayedSpawnVesselForPlayerAsync(clientId);
                await UniTask.Delay(500, DelayType.UnscaledDeltaTime);

                foreach (var clientPair in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (clientPair.ClientId != clientId)
                    {
                        var target = new ClientRpcSendParams { TargetClientIds = new[] { clientPair.ClientId } };
                        clientPlayerVesselInitializer.InitializeNewClientsOwnerPlayerAndVesselInExistingClient_ClientRpc(
                            clientId, new ClientRpcParams { Send = target });
                    }
                    else
                    {
                        var target = new ClientRpcSendParams { TargetClientIds = new[] { clientId } };
                        clientPlayerVesselInitializer.InitializeAllPlayersAndVesselsInThisNewClient_ClientRpc(
                            new ClientRpcParams { Send = target });
                    }
                }
            }
            catch (Exception ex)
            {
                CSDebug.LogError($"[SPVI] Error in DelayedSpawnVesselForPlayer: {ex}");
            }
        }

        protected async UniTask DelayedSpawnVesselForPlayerAsync(ulong clientId)
        {
            await UniTask.Delay(500, DelayType.UnscaledDeltaTime);

            var playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (!playerNetObj)
            {
                CSDebug.LogError($"[SPVI] Player object not found for client {clientId}");
                return;
            }

            var player = playerNetObj.GetComponent<Player>();
            if (!player)
            {
                CSDebug.LogError($"[SPVI] Player component missing on {clientId}");
                return;
            }

            player.NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
            player.NetIsAI.Value = false;

            if (player.NetDefaultVesselType.Value == VesselClassType.Random)
            {
                CSDebug.LogWarning("Vessel type not set, setting default dolphin");
                player.NetDefaultVesselType.Value = VesselClassType.Dolphin;
            }
            SpawnVesselForPlayer(clientId, player);
        }

        void SpawnVesselForPlayer(ulong clientId, Player networkPlayer)
        {
            VesselClassType vesselTypeToSpawn = networkPlayer.NetDefaultVesselType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselTypeToSpawn, out Transform shipPrefabTransform))
            {
                CSDebug.LogError($"[SPVI] No prefab for vessel type {vesselTypeToSpawn}");
                return;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError($"[SPVI] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return;
            }

            var networkVessel = Instantiate(shipNetworkObject);
            networkVessel.SpawnWithOwnership(clientId, true);
            networkPlayer.NetVesselId.Value = networkVessel.NetworkObjectId;
        }
    }
}