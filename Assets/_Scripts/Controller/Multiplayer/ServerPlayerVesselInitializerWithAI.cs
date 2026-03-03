using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Injectors;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Extension of ServerPlayerVesselInitializer:
    /// spawns server-owned AI Player objects, then delegates ALL vessel spawning
    /// (AI and human) to the base class's unified pipeline.
    ///
    /// This mirrors the Menu_Main pattern where MenuServerPlayerVesselInitializer
    /// overrides OnPlayerReadyToSpawnAsync to add post-spawn behavior (autopilot).
    /// Here, the override configures the AIPilot after the base spawns the vessel.
    ///
    /// OnNetworkSpawn flow:
    ///   1. SpawnAIPlayerObjects() — creates AI Player NetworkObjects and sets their
    ///      NetworkVariables (name, vessel type, domain, IsAI). No vessel spawning.
    ///      AI players fire OnPlayerNetworkSpawnedUlong, but the base hasn't
    ///      subscribed yet so the events are harmlessly ignored.
    ///   2. base.OnNetworkSpawn() — subscribes to OnPlayerNetworkSpawnedUlong,
    ///      then ProcessPreExistingPlayers() catches all AI + human players
    ///      and routes them through the standard pipeline:
    ///      preSpawnDelay → SpawnVesselForPlayer → InitializePlayerAndVessel
    ///      → postSpawnDelay → NotifyClients (RPCs to non-host clients).
    ///   3. OnPlayerReadyToSpawnAsync override — after the base spawns and
    ///      initializes each player's vessel, configures AIPilot for AI players.
    /// </summary>
    public class ServerPlayerVesselInitializerWithAI : ServerPlayerVesselInitializer
    {
        [Header("AI Settings")]
        [SerializeField] bool spawnAIOnServerReady = true;

        [Tooltip("NetworkObject prefab that contains your Player component (must be a registered NetworkPrefab).")]
        [SerializeField] NetworkObject aiPlayerPrefab;

        [Tooltip("The data needed to spawn AI")]
        [SerializeField] IPlayer.InitializeData[] aiInitializeDatas;

        [Header("AI Ship Selection")]
        [Tooltip("Game list used to look up available ships for AI opponents. If unset, AI defaults to Sparrow.")]
        [SerializeField] SO_GameList gameList;

        [Header("AI Profiles")]
        [Tooltip("Optional AI profile list for assigning unique names to AI opponents.")]
        [SerializeField] SO_AIProfileList aiProfileList;

        protected override void OnNetworkSpawn()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] OnNetworkSpawn — NOT server, disabling</color>");
                enabled = false;
                return;
            }

            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] OnNetworkSpawn — IsServer=true, RequestedAIBackfill={gameData.RequestedAIBackfillCount}, spawnAIOnServerReady={spawnAIOnServerReady}</color>");

            // Fresh domain pool before any player/AI spawning.
            // Previous session's pool state is stale after scene transition.
            DomainAssigner.Initialize();

            // Set scene-specific spawn positions before AI spawning.
            // base.OnNetworkSpawn() also sets them, but AI Player objects need
            // spawn positions configured first (for AddPlayer → SetPoseOfVessel).
            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                gameData.SetSpawnPositions(playerSpawnPoints);

            // Spawn AI Player objects BEFORE subscribing to OnPlayerNetworkSpawnedUlong.
            // AI players fire the event when their NetworkVariables are set
            // (via TryRaiseDeferredSpawnEvent), but since we haven't subscribed yet
            // (base.OnNetworkSpawn hasn't run), those events are harmlessly ignored.
            // The base's ProcessPreExistingPlayers() catches them afterwards.
            // Wrapped in try-catch to guarantee base.OnNetworkSpawn() always
            // runs — otherwise no human players would be processed.
            if (spawnAIOnServerReady)
            {
                try
                {
                    Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling SpawnAIPlayerObjects()</color>");
                    SpawnAIPlayerObjects();
                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIPlayerObjects() complete. gameData.Players.Count={gameData.Players.Count}</color>");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIPlayerObjects FAILED: {e.Message}\n{e.StackTrace}</color>");
                    CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] SpawnAIPlayerObjects failed: {e.Message}");
                }
            }

            // Subscribe and process ALL players (AI + human) through the unified
            // pipeline. ProcessPreExistingPlayers() catches AI players already in
            // gameData.Players and routes them through HandlePlayerNetworkSpawnedAsync
            // → OnPlayerReadyToSpawnAsync (our override) → ConfigureAIPilot.
            Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling base.OnNetworkSpawn() — unified pipeline for all players</color>");
            base.OnNetworkSpawn();
        }

        /// <summary>
        /// After the base spawns and initializes the vessel, configure AIPilot for AI players.
        /// Same pattern as MenuServerPlayerVesselInitializer.OnPlayerReadyToSpawnAsync
        /// which calls ActivateAutopilot after base.
        /// </summary>
        protected override async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            await base.OnPlayerReadyToSpawnAsync(player, ct);

            if (player.NetIsAI.Value)
                ConfigureAIPilot(player);
        }

        /// <summary>
        /// Spawns AI Player NetworkObjects and sets their NetworkVariables.
        /// Does NOT spawn vessels — the base class pipeline handles that uniformly
        /// for both AI and human players via SpawnVesselForPlayer + NotifyClients.
        /// </summary>
        void SpawnAIPlayerObjects()
        {
            if (!aiPlayerPrefab)
            {
                Debug.LogError("<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] aiPlayerPrefab is NOT assigned!</color>");
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                return;
            }

            int aiCount = gameData.EnsureMinimumAIBackfill();
            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIPlayerObjects — aiCount={aiCount}</color>");
            if (aiCount <= 0)
            {
                Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] No AI to spawn (aiCount <= 0)</color>");
                return;
            }

            // Use AI profile list for names when available; fall back to aiInitializeDatas templates.
            System.Collections.Generic.List<AIProfile> profiles = null;
            if (aiProfileList != null)
                profiles = aiProfileList.PickRandom(aiCount);

            for (int i = 0; i < aiCount; i++)
            {
                var aiPlayerNO = Instantiate(aiPlayerPrefab);
                GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);

                aiPlayerNO.Spawn(true);

                var aiPlayer = aiPlayerNO.GetComponent<Player>();
                if (!aiPlayer)
                {
                    CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] AI Player prefab missing Player component.");
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                // Use template data if available, otherwise derive values dynamically
                var hasTemplate = aiInitializeDatas != null && i < aiInitializeDatas.Length;

                var aiVesselType = hasTemplate ? aiInitializeDatas[i].vesselClass : VesselClassType.Random;
                if (aiVesselType is VesselClassType.Any or VesselClassType.Random)
                    aiVesselType = PickAIVesselType();

                var aiName = profiles != null && i < profiles.Count
                    ? profiles[i].Name
                    : hasTemplate ? aiInitializeDatas[i].PlayerName : $"AI {i + 1}";

                var aiDomain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);

                aiPlayer.NetDefaultVesselType.Value = aiVesselType;
                aiPlayer.NetName.Value = aiName;
                aiPlayer.NetDomain.Value = aiDomain;
                aiPlayer.NetIsAI.Value = true;

                Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] AI Player {i} created: Name={aiName}, Vessel={aiVesselType}, Domain={aiDomain}</color>");
            }
        }

        VesselClassType PickAIVesselType()
        {
            if (gameList != null)
            {
                var game = FindGameByMode(gameData.GameMode);
                if (game != null && game.Captains is { Count: > 0 })
                {
                    var captain = game.Captains[Random.Range(0, game.Captains.Count)];
                    if (captain?.Ship != null && vesselPrefabContainer.TryGetShipPrefab(captain.Ship.Class, out _))
                        return captain.Ship.Class;
                }
            }
            return VesselClassType.Sparrow;
        }

        SO_ArcadeGame FindGameByMode(GameModes mode)
        {
            if (gameList?.Games == null) return null;
            foreach (var game in gameList.Games)
            {
                if (game.Mode == mode) return game;
            }
            return null;
        }

        void ConfigureAIPilot(Player player)
        {
            if (player.Vessel == null) return;

            var vesselMono = player.Vessel as MonoBehaviour;
            if (vesselMono == null) return;

            var aiPilot = vesselMono.GetComponentInChildren<AIPilot>();
            if (aiPilot == null) return;

            bool shouldSeekPlayers = gameData.GameMode == GameModes.MultiplayerJoust;
            float skill = Mathf.Clamp01(gameData.SelectedIntensity.Value * 0.25f);
            aiPilot.ConfigureForGameMode(gameData, shouldSeekPlayers, skill);
        }
    }
}
