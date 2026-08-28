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
            // so a player who picked any domain in GameDataSO.ActiveDomains is never reassigned.
            var activeDomains = BuildActiveDomains(humanDomains, ResolveRequestedDomainCount());
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

            int aiCount = ResolveAICount();
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

                var aiDomain = ResolveAIDomain(counts);
                // counts is seeded only from ActiveDomains (Jade/Ruby/Gold) — a domain
                // outside that set (e.g. Friction hunters using Domains.Blue) won't have
                // an entry yet, so increment safely rather than assuming the key exists.
                counts[aiDomain] = counts.GetValueOrDefault(aiDomain) + 1;

                aiPlayer.NetDefaultVesselType.Value = aiVesselType;
                aiPlayer.NetName.Value = aiName;
                aiPlayer.NetDomain.Value = aiDomain;
                aiPlayer.NetIsAI.Value = true;

                if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
                {
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                OnAIVesselSpawned(aiVesselNO, aiPlayer);

                // Server-side initialization of the AI player-vessel pair
                if (!aiVesselNO.TryGetComponent(out IVessel vessel))
                {
                    CSDebug.LogError("[ClientPlayerVesselInitializer] Spawned vessel missing IVessel component.");
                    return;
                }

                clientPlayerVesselInitializer.InitializePlayerAndVessel(aiPlayer, vessel);
                ConfigureAIPilot(aiVesselNO);
                OnAIPlayerInitialized(aiPlayer, i, aiCount);
            }
        }

        /// <summary>
        /// How many AI to spawn. Default: team-balancing backfill (desired total
        /// players minus humans present). Friction overrides this with a fixed
        /// per-intensity hunter roster instead.
        /// </summary>
        protected virtual int ResolveAICount() => gameData.RequestedAIBackfillCount;

        /// <summary>
        /// Which domain a newly-spawned AI joins. Default: the standard team-balancing
        /// pick. Friction overrides this to always return <see cref="Domains.Blue"/>
        /// (the "no specific team" sentinel) since hunters aren't allied with any human
        /// team — this also makes <see cref="AIPilot"/>'s same-domain skip in
        /// UpdatePlayerTarget naturally exclude hunters from targeting each other.
        /// </summary>
        protected virtual Domains ResolveAIDomain(Dictionary<Domains, int> counts) => GetBalancedDomain(counts);

        /// <summary>
        /// AI skill level (0-1) fed into AIPilot.ConfigureForGameMode. Default: scales
        /// with selected intensity. Friction overrides this with its own 4-level curve.
        /// </summary>
        protected virtual float ResolveAISkill() => Mathf.Clamp01(gameData.SelectedIntensity.Value * 0.25f);

        /// <summary>
        /// Called once an AI's vessel NetworkObject has spawned, before pair
        /// initialization and pilot configuration. Default: no-op. Friction overrides
        /// this to attach <see cref="FrictionHunterTag"/> to the spawned instance only —
        /// never to the shared vessel prefab asset — so hunter-only impact effects
        /// (e.g. VesselLifeLossByHunterSkimmerEffectSO) stay inert for every other mode's
        /// Rhino players.
        /// </summary>
        protected virtual void OnAIVesselSpawned(NetworkObject aiVesselNO, Player aiPlayer) { }

        /// <summary>
        /// Called once an AI's player-vessel pair is fully initialized and its pilot
        /// configured. Default: no-op. This runs after
        /// <see cref="GameDataSO.AddPlayer"/> has already handed the AI a pose from the
        /// shared player spawn pool, so it is the earliest point an override can place an
        /// AI somewhere of its own choosing without being overwritten — Friction uses it
        /// to scatter hunters around the arena instead of stacking them on the human
        /// spawn cluster. <paramref name="aiIndex"/> and <paramref name="aiCount"/>
        /// describe this AI's slot in the roster so placement can be distributed
        /// deterministically.
        /// </summary>
        protected virtual void OnAIPlayerInitialized(Player aiPlayer, int aiIndex, int aiCount) { }

        /// <summary>
        /// Returns the active domain with the fewest players. Ties are broken
        /// deterministically by <see cref="GameDataSO.ActiveDomains"/> enum order
        /// (Jade → Ruby → Gold), so identical inputs produce identical AI
        /// distributions across machines without needing a shared RNG seed.
        /// </summary>
        protected static Domains GetBalancedDomain(Dictionary<Domains, int> counts)
        {
            int min = int.MaxValue;
            foreach (var v in counts.Values)
                if (v < min) min = v;

            // Iterate ActiveDomains in order so the first match (== smallest team
            // with the lowest enum index) wins ties deterministically.
            foreach (var d in GameDataSO.ActiveDomains)
                if (counts.TryGetValue(d, out var c) && c == min)
                    return d;

            // Should never happen if counts is initialized from BuildInitialCounts,
            // but degrade gracefully rather than throw.
            CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] GetBalancedDomain: counts dict is empty");
            return GameDataSO.ActiveDomains[0];
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
        /// Resolves the domain count the AI-fill pass should target.
        ///
        /// A stored count of 1 is the arcade stepper's "solo" option: the human keeps
        /// their own domain and every AI is placed on one opposing domain, so the match
        /// is player-vs-AI rather than everyone sharing a single team. Returning 1
        /// unchanged would put the AI on the human's domain and produce co-op.
        ///
        /// Co-op-vs-environment modes are exempt — there the AI backfill are teammates,
        /// so a single shared domain is the intended result.
        /// </summary>
        int ResolveRequestedDomainCount()
        {
            int requested = gameData.RequestedDomainCount;
            if (requested > 1) return requested;

            if (gameData.GameMode == GameModes.MultiplayerWildlifeBlitzGame)
                return 1;

            return Mathf.Min(2, GameDataSO.ActiveDomains.Length);
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

            bool shouldSeekPlayers = gameData.GameMode is GameModes.MultiplayerJoust or GameModes.Friction;
            float skill = ResolveAISkill();
            aiPilot.ConfigureForGameMode(gameData, shouldSeekPlayers, skill);
        }
    }
}
