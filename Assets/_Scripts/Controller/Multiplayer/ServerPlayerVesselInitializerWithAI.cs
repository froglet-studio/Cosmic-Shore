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

            // Always normalize human domains — even with 0 AI backfill (solo play).
            // Without this, a player who picked "Random" (Unassigned) or never clicked
            // a team keeps NetDomain = Unassigned, and the scoreboard falls through
            // to the single-player banner color (blue) instead of the user's choice.
            NormalizeHumanDomains(GatherHumanPlayers());

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

            // Human domains were already normalized in OnNetworkSpawn (runs for solo play too).
            // Re-gather here to build team counts from the authoritative ConnectedClients list —
            // gameData.Players is cleared by ResetRuntimeData during scene transition.
            var humanPlayers = GatherHumanPlayers();

            // Use AI profile list for names when available; fall back to aiInitializeDatas templates.
            List<AIProfile> profiles = null;
            if (aiProfileList != null)
                profiles = aiProfileList.PickRandom(aiCount);

            // Build team counts from the gathered human players (not gameData.Players
            // which may be empty at this point in the spawn sequence).
            var teamCounts = BuildTeamCountsFromPlayers(humanPlayers);

            for (int i = 0; i < aiCount; i++)
            {
                var aiPlayerNO = Instantiate(aiPlayerPrefab);
                GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);

                // destroyWithScene=false — AI spawns in same tick as scene load; see ClearPlayerVesselReferences for cleanup.
                aiPlayerNO.Spawn(false);

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
        /// Gathers human Player objects from NetworkManager.ConnectedClients.
        /// gameData.Players is empty at this point (cleared by ResetRuntimeData
        /// during scene transition), so we must go directly to Netcode.
        /// </summary>
        List<Player> GatherHumanPlayers()
        {
            var humans = new List<Player>();
            var nm = NetworkManager.Singleton;
            if (nm == null) return humans;

            foreach (var kvp in nm.ConnectedClients)
            {
                var playerObj = kvp.Value.PlayerObject;
                if (playerObj == null) continue;
                if (!playerObj.TryGetComponent<Player>(out var player)) continue;
                if (player.NetIsAI.Value) continue;
                humans.Add(player);
            }

            Debug.Log($"<color=#FF00FF>[FLOW-5AI] GatherHumanPlayers: found {humans.Count} humans</color>");
            return humans;
        }

        /// <summary>
        /// Builds the list of active team domains based on the human's chosen domain
        /// and the requested team count. The human's domain is always slot 0;
        /// remaining slots are filled from the standard pool (Jade, Ruby, Gold)
        /// excluding the human's domain.
        /// </summary>
        static List<Domains> BuildActiveTeams(Domains humanDomain, int teamCount)
        {
            var teams = new List<Domains> { humanDomain };

            foreach (var d in GameDataSO.TeamDomains)
            {
                if (teams.Count >= teamCount) break;
                if (d != humanDomain)
                    teams.Add(d);
            }

            return teams;
        }

        /// <summary>
        /// Ensures all human players share one team.
        /// The first human's chosen domain is respected (even if it's Ruby or Gold).
        /// Called on the server before AI spawning so team counts are accurate.
        /// </summary>
        void NormalizeHumanDomains(List<Player> humans)
        {
            // Find the first human player's chosen domain
            Domains partyDomain = Domains.Unassigned;
            foreach (var player in humans)
            {
                var domain = player.NetDomain.Value;
                if (domain != Domains.Unassigned && domain != Domains.None)
                {
                    partyDomain = domain;
                    break;
                }
            }

            // If no valid domain found, fall back to Jade
            if (partyDomain == Domains.Unassigned)
                partyDomain = GameDataSO.TeamDomains[0];

            // Assign all human players to the party domain
            foreach (var player in humans)
            {
                if (player.NetDomain.Value != partyDomain)
                {
                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] NormalizeHumanDomains: Reassigning {player.NetName.Value} from {player.NetDomain.Value} to {partyDomain}</color>");
                    player.NetDomain.Value = partyDomain;
                }
            }

            Debug.Log($"<color=#FF00FF>[FLOW-5AI] NormalizeHumanDomains: {humans.Count} humans → domain={partyDomain}, teamCount={gameData.RequestedTeamCount}</color>");
        }

        /// <summary>
        /// Builds team counts from the given human players.
        /// Active teams are built around the human's domain (not hardcoded Jade-first).
        /// </summary>
        Dictionary<Domains, int> BuildTeamCountsFromPlayers(List<Player> humans)
        {
            int teamCount = Mathf.Clamp(gameData.RequestedTeamCount, 1, GameDataSO.TeamDomains.Length);

            // Determine the human party domain
            Domains humanDomain = GameDataSO.TeamDomains[0];
            foreach (var player in humans)
            {
                var d = player.NetDomain.Value;
                if (d != Domains.Unassigned && d != Domains.None)
                {
                    humanDomain = d;
                    break;
                }
            }

            var activeTeams = BuildActiveTeams(humanDomain, teamCount);
            var counts = new Dictionary<Domains, int>();
            foreach (var team in activeTeams)
                counts[team] = 0;

            foreach (var player in humans)
            {
                var domain = player.NetDomain.Value;
                if (counts.ContainsKey(domain))
                    counts[domain]++;
                else
                    counts[activeTeams[0]]++;
            }

            Debug.Log($"<color=#FF00FF>[FLOW-5AI] BuildTeamCountsFromPlayers: {string.Join(", ", counts)}</color>");
            return counts;
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
            // destroyWithScene=false matches the AI player spawn — must stay consistent for cleanup ordering.
            vesselNO.Spawn(false);
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
