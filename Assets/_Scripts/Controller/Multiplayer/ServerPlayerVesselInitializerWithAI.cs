using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Reflex.Injectors;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Extension of ServerPlayerVesselInitializer:
    /// spawns server-owned AI players and their vessels, then delegates
    /// human player handling to the base class via OnPlayerNetworkSpawnedUlong.
    ///
    /// OnNetworkSpawn flow:
    ///   1. SpawnAIs() — creates AI players + vessels (fires OnPlayerNetworkSpawnedUlong
    ///      for each, but we haven't subscribed yet so the base ignores them)
    ///   2. Mark AI players in _processedPlayers so the base never processes them
    ///   3. base.OnNetworkSpawn() — subscribes to event + handles human players going forward
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
        [Inject] SO_GameList gameList;

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
            // base.OnNetworkSpawn() also sets them, but AI spawns happen first
            // (before base runs), so positions must be configured here.
            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                gameData.SetSpawnPositions(playerSpawnPoints);

            // Spawn AIs BEFORE subscribing to OnPlayerNetworkSpawnedUlong.
            // AI players fire the event during Spawn(), but since we haven't
            // subscribed yet (base.OnNetworkSpawn hasn't run), those events
            // are harmlessly ignored by the base.
            // Wrapped in try-catch to guarantee base.OnNetworkSpawn() always
            // runs — otherwise no human players would be processed.
            if (spawnAIOnServerReady)
            {
                try
                {
                    Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling SpawnAIs()</color>");
                    SpawnAIs();
                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs() complete. gameData.Players.Count={gameData.Players.Count}</color>");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs FAILED: {e.Message}\n{e.StackTrace}</color>");
                    CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] SpawnAIs failed: {e.Message}");
                }
            }

            // Mark all AI players as processed so the base skips them
            int aiMarked = 0;
            foreach (var p in gameData.Players)
            {
                if (p is Player aiPlayer && aiPlayer.NetIsAI.Value)
                {
                    _processedPlayers.Add(aiPlayer.NetworkObjectId);
                    aiMarked++;
                }
            }
            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Marked {aiMarked} AI players as processed. Calling base.OnNetworkSpawn()</color>");

            // Now subscribe (via base) and handle human players going forward
            base.OnNetworkSpawn();
        }

        void SpawnAIs()
        {
            if (!aiPlayerPrefab)
            {
                Debug.LogError("<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] aiPlayerPrefab is NOT assigned!</color>");
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                return;
            }

            int aiCount = gameData.RequestedAIBackfillCount;
            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs — aiCount={aiCount}, teamCount={gameData.RequestedTeamCount}</color>");
            if (aiCount <= 0)
            {
                Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] No AI to spawn (aiCount <= 0)</color>");
                return;
            }

            // Ensure all human players have valid domains for the configured team count.
            // Party members are placed on the same team (first human's domain wins).
            NormalizeHumanDomains();

            // Use AI profile list for names when available; fall back to aiInitializeDatas templates.
            List<AIProfile> profiles = null;
            if (aiProfileList != null)
                profiles = aiProfileList.PickRandom(aiCount);

            // Build team counts from existing human players so AI fills the smallest teams first.
            var teamCounts = gameData.BuildTeamCounts();

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

                var aiDomain = GetBalancedDomain(teamCounts);
                teamCounts[aiDomain]++;

                aiPlayer.NetDefaultVesselType.Value = aiVesselType;
                aiPlayer.NetName.Value = aiName;
                aiPlayer.NetDomain.Value = aiDomain;
                aiPlayer.NetIsAI.Value = true;

                if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
                {
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                // Server-side initialization of the AI player-vessel pair
                if (!aiVesselNO.TryGetComponent(out IVessel vessel))
                {
                    CSDebug.LogError("[ClientPlayerVesselInitializer] Spawned vessel missing IVessel component.");
                    return;
                }

                clientPlayerVesselInitializer.InitializePlayerAndVessel(aiPlayer, vessel);
                ConfigureAIPilot(aiVesselNO);
            }
        }

        /// <summary>
        /// Returns the domain with the fewest players. Ties broken by enum order (Jade first).
        /// </summary>
        static Domains GetBalancedDomain(Dictionary<Domains, int> counts)
        {
            Domains best = Domains.Jade;
            int bestCount = int.MaxValue;

            foreach (var kvp in counts)
            {
                if (kvp.Value < bestCount)
                {
                    bestCount = kvp.Value;
                    best = kvp.Key;
                }
            }

            return best;
        }

        /// <summary>
        /// Ensures all human players have domains within the active team set
        /// (based on RequestedTeamCount) and that party members share one team.
        /// Called on the server before AI spawning so BuildTeamCounts is accurate.
        /// </summary>
        void NormalizeHumanDomains()
        {
            int teamCount = Mathf.Clamp(gameData.RequestedTeamCount, 1, 3);

            // Find the first human player's chosen domain
            Domains partyDomain = Domains.Unassigned;
            foreach (var p in gameData.Players)
            {
                if (p is Player player && !player.NetIsAI.Value)
                {
                    var domain = player.NetDomain.Value;
                    if (domain != Domains.Unassigned && domain != Domains.None)
                    {
                        partyDomain = domain;
                        break;
                    }
                }
            }

            // If no human has a valid domain, pick the first team
            if (partyDomain == Domains.Unassigned)
                partyDomain = GameDataSO.TeamDomains[0];

            // When multiple teams are active, ensure the party domain is within
            // the valid team set. Single-team mode always respects the player's choice.
            if (teamCount > 1)
            {
                var validDomains = new HashSet<Domains>();
                for (int i = 0; i < teamCount; i++)
                    validDomains.Add(GameDataSO.TeamDomains[i]);

                if (!validDomains.Contains(partyDomain))
                    partyDomain = GameDataSO.TeamDomains[0];
            }

            // Assign all human players to the party domain
            foreach (var p in gameData.Players)
            {
                if (p is Player player && !player.NetIsAI.Value)
                {
                    if (player.NetDomain.Value != partyDomain)
                    {
                        Debug.Log($"<color=#FF00FF>[FLOW-5AI] NormalizeHumanDomains: Reassigning {player.NetName.Value} from {player.NetDomain.Value} to {partyDomain}</color>");
                        player.NetDomain.Value = partyDomain;
                    }
                }
            }
        }

        VesselClassType PickAIVesselType()
        {
            if (gameList != null)
            {
                var game = FindGameByMode(gameData.GameMode);
                if (game != null && game.Vessels is { Count: > 0 })
                {
                    var vessel = game.Vessels[Random.Range(0, game.Vessels.Count)];
                    if (vessel != null && vesselPrefabContainer.TryGetShipPrefab(vessel.Class, out _))
                        return vessel.Class;
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

        bool TrySpawnVesselForAI(Player aiPlayer, out NetworkObject vesselNO)
        {
            vesselNO = null;
            var vesselType = aiPlayer.NetDefaultVesselType.Value;

            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefabTransform))
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] No prefab for AI vessel type {vesselType}");
                return false;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError($"[ServerPlayerVesselInitializerWithAI] Prefab {shipPrefabTransform.name} missing NetworkObject");
                return false;
            }

            vesselNO = Instantiate(shipNetworkObject);
            GameObjectInjector.InjectRecursive(vesselNO.gameObject, _container);
            vesselNO.Spawn(true);
            aiPlayer.NetVesselId.Value = vesselNO.NetworkObjectId;
            return true;
        }

        void ConfigureAIPilot(NetworkObject aiVesselNO)
        {
            var aiPilot = aiVesselNO.GetComponentInChildren<AIPilot>();
            if (aiPilot == null) return;

            bool shouldSeekPlayers = gameData.GameMode == GameModes.MultiplayerJoust;
            float skill = Mathf.Clamp01(gameData.SelectedIntensity.Value * 0.25f);
            aiPilot.ConfigureForGameMode(gameData, shouldSeekPlayers, skill);
        }
    }
}
