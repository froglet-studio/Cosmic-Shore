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

            // Snapshot human domain selections before any spawning. Holding a snapshot
            // (instead of re-reading NetDomain.Value across the method) means a client
            // firing RequestSetDomain_ServerRpc mid-spawn can't perturb the count balance
            // we hand to AI placement.
            var humans = GatherHumanPlayers();
            var humanDomains = new Dictionary<ulong, Domains>(humans.Count);
            foreach (var h in humans)
                humanDomains[h.NetworkObjectId] = h.NetDomain.Value;

            // Build the active-domain set from humans' choices, expanded as needed
            // so a player who picked a valid domain (Jade/Ruby/Gold) is never reassigned.
            var activeDomains = BuildActiveDomains(humanDomains, gameData.RequestedDomainCount);
            var counts = BuildInitialCounts(humanDomains, activeDomains);

            // Normalize humans whose domain isn't in the active set (Unassigned, None, Blue).
            // They get a balanced assignment via the same algorithm AI uses.
            NormalizeUnassignedHumans(humans, humanDomains, counts);

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
                    SpawnAIs(counts);
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

        void SpawnAIs(Dictionary<Domains, int> counts)
        {
            if (!aiPlayerPrefab)
            {
                Debug.LogError("<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] aiPlayerPrefab is NOT assigned!</color>");
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                return;
            }

            int aiCount = gameData.RequestedAIBackfillCount;
            Debug.Log($"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs — aiCount={aiCount}, domainCount={gameData.RequestedDomainCount}, counts={string.Join(", ", counts)}</color>");
            if (aiCount <= 0)
            {
                Debug.Log("<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] No AI to spawn (aiCount <= 0)</color>");
                return;
            }

            // Use AI profile list for names when available; fall back to aiInitializeDatas templates.
            List<AIProfile> profiles = null;
            if (aiProfileList != null)
                profiles = aiProfileList.PickRandom(aiCount);

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

                var aiDomain = GetBalancedDomain(counts);
                counts[aiDomain]++;

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
        /// Returns one of the active domains tied for the fewest players. When a single
        /// domain has the lowest count it's returned outright; when multiple domains tie
        /// at the minimum, one is chosen uniformly at random.
        /// Server-only — runs on the host, so all clients see the same outcome via
        /// NetDomain replication.
        /// </summary>
        static Domains GetBalancedDomain(Dictionary<Domains, int> counts)
        {
            int min = int.MaxValue;
            foreach (var v in counts.Values)
                if (v < min) min = v;

            // Collect every active domain tied at the minimum count. Iterate
            // ActiveDomains (not counts.Keys) so the candidate list has a stable
            // order regardless of dictionary hashing; the random pick below is
            // the only source of nondeterminism.
            _tiedBuf.Clear();
            foreach (var d in GameDataSO.ActiveDomains)
                if (counts.TryGetValue(d, out var c) && c == min)
                    _tiedBuf.Add(d);

            if (_tiedBuf.Count == 0)
            {
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] GetBalancedDomain: no active domains in counts");
                return GameDataSO.ActiveDomains[0];
            }

            return _tiedBuf[UnityEngine.Random.Range(0, _tiedBuf.Count)];
        }

        // Reused per call to keep AI spawning allocation-free. Server-only access on
        // the main thread — no concurrency concerns.
        static readonly List<Domains> _tiedBuf = new(GameDataSO.ActiveDomains.Length);

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

        static bool IsActiveDomain(Domains d)
        {
            foreach (var a in GameDataSO.ActiveDomains)
                if (a == d) return true;
            return false;
        }

        static int IndexInActiveDomains(Domains d)
        {
            for (int i = 0; i < GameDataSO.ActiveDomains.Length; i++)
                if (GameDataSO.ActiveDomains[i] == d) return i;
            return int.MaxValue; // unknown domains sort last
        }

        /// <summary>
        /// Builds the active-domain set: all valid domains humans chose (sorted by
        /// count desc, with <see cref="GameDataSO.ActiveDomains"/> index as tie-break),
        /// expanded to at least <paramref name="requestedDomainCount"/> by padding from
        /// the standard pool, capped at the pool size. Humans never get displaced —
        /// if they picked more distinct domains than requested, the active set grows
        /// rather than reassigning anyone.
        /// </summary>
        static List<Domains> BuildActiveDomains(Dictionary<ulong, Domains> humanDomains, int requestedDomainCount)
        {
            var chosenCounts = new Dictionary<Domains, int>();
            foreach (var d in humanDomains.Values)
            {
                if (!IsActiveDomain(d)) continue;
                chosenCounts[d] = chosenCounts.TryGetValue(d, out var c) ? c + 1 : 1;
            }

            var ordered = new List<Domains>(chosenCounts.Keys);
            ordered.Sort((a, b) =>
            {
                int cmp = chosenCounts[b].CompareTo(chosenCounts[a]);
                if (cmp != 0) return cmp;
                return IndexInActiveDomains(a).CompareTo(IndexInActiveDomains(b));
            });

            int effective = Mathf.Clamp(
                Mathf.Max(requestedDomainCount, ordered.Count),
                1, GameDataSO.ActiveDomains.Length);

            var active = new List<Domains>(ordered);
            foreach (var d in GameDataSO.ActiveDomains)
            {
                if (active.Count >= effective) break;
                if (!active.Contains(d)) active.Add(d);
            }

            Debug.Log($"<color=#FF00FF>[FLOW-5AI] BuildActiveDomains: requested={requestedDomainCount}, effective={effective}, domains=[{string.Join(", ", active)}]</color>");
            return active;
        }

        static Dictionary<Domains, int> BuildInitialCounts(
            Dictionary<ulong, Domains> humanDomains,
            List<Domains> activeDomains)
        {
            var counts = new Dictionary<Domains, int>();
            foreach (var d in activeDomains) counts[d] = 0;
            foreach (var d in humanDomains.Values)
                if (counts.ContainsKey(d)) counts[d]++;
            return counts;
        }

        /// <summary>
        /// Assigns a balanced domain to each human whose snapshot domain is not in
        /// the active set (Unassigned, None, Blue). Writes the new domain server-side
        /// (NetDomain is server-write since the permission flip), updates the snapshot,
        /// and bumps counts so AI placement sees the result.
        /// </summary>
        void NormalizeUnassignedHumans(
            List<Player> humans,
            Dictionary<ulong, Domains> humanDomains,
            Dictionary<Domains, int> counts)
        {
            int reassigned = 0;
            foreach (var h in humans)
            {
                var d = humanDomains[h.NetworkObjectId];
                if (counts.ContainsKey(d)) continue;

                var assigned = GetBalancedDomain(counts);
                counts[assigned]++;
                h.NetDomain.Value = assigned;
                humanDomains[h.NetworkObjectId] = assigned;
                reassigned++;
                Debug.Log($"<color=#FF00FF>[FLOW-5AI] NormalizeUnassignedHumans: assigned {h.NetName.Value} ({d}) → {assigned}</color>");
            }

            Debug.Log($"<color=#FF00FF>[FLOW-5AI] NormalizeUnassignedHumans: {reassigned}/{humans.Count} humans reassigned, counts={string.Join(", ", counts)}</color>");
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
