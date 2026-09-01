using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
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
    ///   1. SpawnAIs() - creates AI players + vessels (fires OnPlayerNetworkSpawnedUlong
    ///      for each, but we haven't subscribed yet so the base ignores them)
    ///   2. Mark AI players in _processedPlayers so the base never processes them
    ///   3. base.OnNetworkSpawn() - subscribes to event + handles human players going forward
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

        // Tournament standings are keyed by player name, but AI Player NetworkObjects are
        // destroyed and re-spawned every minigame scene - so the AI roster must stay stable
        // across the lineup. The names are seeded once (first game) into TournamentDataSO and
        // reused for every subsequent game.
        [Inject] TournamentDataSO tournamentData;

        protected override void OnNetworkSpawn()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] OnNetworkSpawn - NOT server, disabling</color>");
                enabled = false;
                return;
            }

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] OnNetworkSpawn - IsServer=true, RequestedAIBackfill={gameData.RequestedAIBackfillCount}, spawnAIOnServerReady={spawnAIOnServerReady}</color>");

            // Set scene-specific spawn positions before AI spawning.
            // base.OnNetworkSpawn() also sets them, but AI spawns happen first
            // (before base runs), so positions must be configured here.
            // The cell-relative ring is built on first vessel spawn instead (it needs the cell's
            // nucleus), so skip the authored transforms entirely when it is enabled.
            if (!arrangeSpawnPointsAroundCell && playerSpawnPoints != null && playerSpawnPoints.Length > 0)
                gameData.SetSpawnPositions(playerSpawnPoints);
            else
                EnsureSpawnPosesReady(); // AI draw poses during SpawnAIs below - the ring must exist first

            // A HUMAN'S PICK WIDENS THE MATCH, so the domain count covers every domain someone
            // actually chose before anything is derived from it.
            //
            // This was the shipped defect. The active set is the contiguous slice
            // ActiveDomains[0..DC-1] and DC is clamped to the PLAYER COUNT
            // (ArcadeGameConfigureModal.ComputeMaxDomainCount), so a 1-human + 1-AI lobby has
            // DC 2 and an active set of {Jade, Ruby} - and a pilot who picked GOLD in the lobby
            // was silently reassigned off it here, at spawn, by NormalizeUnassignedHumans, with
            // the launch UI having accepted the pick and shown it. Jade is always slice[0], so
            // they landed on Jade, whose authored palette is teal-and-blue: reported as
            // "I selected gold and got the blue domain".
            //
            // It is the SAME principle the AI placement below already states in its own words -
            // "placing on Gold in a two-domain lobby is the host widening the match, and
            // re-balancing it away silently is exactly the 'cannot add to gold' playtest defect" -
            // applied to the half that was left behind. RequestSetDomain_ServerRpc deliberately
            // accepts any playable domain regardless of DC, because "the domain count is a
            // property of how the MATCH is scored, not a gate on which colour a player may fly";
            // that promise only holds if the pick survives spawn.
            //
            // Raising the COUNT rather than widening a local list is what keeps every consumer
            // agreeing: ScoringRuleSO sums over the same ActiveDomains[0..DC-1] prefix, so a Gold
            // pilot in a DC-2 match would otherwise be spawned Gold and then never scored. The set
            // is an ordered prefix, so covering Gold necessarily means DC 3.
            //
            // It can only ever WIDEN on a real pick: NetDomain's sole writer is that RPC, which
            // rejects Blue, and its initializer is Jade - already slice[0].
            var humans = GatherHumanPlayers();
            foreach (var h in humans)
            {
                if (h == null || h.NetIsAI.Value) continue;
                int idx = System.Array.IndexOf(GameDataSO.ActiveDomains, h.NetDomain.Value);
                if (idx < 0) continue;
                // Capped at the MODE's own limit, never just the playable set: the host's
                // DomainCount is a preference a pick may widen, but Astro League's two goals and
                // Brood Rush's two-domain shape are RULES, and a pick must not widen past those.
                // A pick BEYOND the cap is still rebalanced by NormalizeUnassignedHumans below,
                // and that is correct rather than a leftover of the bug: the mode genuinely
                // cannot seat a third team, so the pick is one it can never honour. The defect
                // was overriding a pick the mode COULD have honoured and the host had merely not
                // asked for.
                int cap = Mathf.Clamp(gameData.MaxDomainsForGame, 1, GameDataSO.ActiveDomains.Length);
                if (idx + 1 > gameData.RequestedDomainCount && idx + 1 <= cap)
                    gameData.RequestedDomainCount = idx + 1;
            }

            var activeDomains = BuildActiveDomains(gameData.RequestedDomainCount);

            // Two dicts for AI placement:
            //   humanCounts: how many humans are on each active domain (fixed across SpawnAIs)
            //   totalCounts: humans + AI assigned so far (mutated as AI spawn)
            // GetBalancedDomain breaks ties by lowest total, then fewest humans, then enum order.
            var humanCounts = GameDataSO.BuildHumanCounts(humans, activeDomains);
            var totalCounts = new Dictionary<Domains, int>(humanCounts);

            // Normalize humans whose domain isn't in the active set. They get a
            // balanced assignment via the same algorithm AI uses, and both dicts
            // are bumped to reflect the new placement.
            NormalizeUnassignedHumans(humans, activeDomains, totalCounts, humanCounts);

            // Spawn AIs BEFORE subscribing to OnPlayerNetworkSpawnedUlong.
            // AI players fire the event during Spawn(), but since we haven't
            // subscribed yet (base.OnNetworkSpawn hasn't run), those events
            // are harmlessly ignored by the base.
            // Wrapped in try-catch to guarantee base.OnNetworkSpawn() always
            // runs - otherwise no human players would be processed.
            if (spawnAIOnServerReady)
            {
                try
                {
                    CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Calling SpawnAIs()</color>");
                    SpawnAIs(totalCounts, humanCounts);
                    CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs() complete. gameData.Players.Count={gameData.Players.Count}</color>");
                }
                catch (System.Exception e)
                {
                    CSDebug.LogError($"<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs FAILED: {e.Message}\n{e.StackTrace}</color>");
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
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] Marked {aiMarked} AI players as processed. Calling base.OnNetworkSpawn()</color>");

            // Now subscribe (via base) and handle human players going forward
            base.OnNetworkSpawn();
        }

        void SpawnAIs(Dictionary<Domains, int> totalCounts, Dictionary<Domains, int> humanCounts)
        {
            if (!aiPlayerPrefab)
            {
                CSDebug.LogError("<color=#FF0000>[FLOW-5AI] [ServerVesselInitWithAI] aiPlayerPrefab is NOT assigned!</color>");
                CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                return;
            }

            int aiCount = gameData.RequestedAIBackfillCount;
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] SpawnAIs - aiCount={aiCount}, domainCount={gameData.RequestedDomainCount}, totals={string.Join(", ", totalCounts)}, humans={string.Join(", ", humanCounts)}</color>");
            if (aiCount <= 0)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FF00FF>[FLOW-5AI] [ServerVesselInitWithAI] No AI to spawn (aiCount <= 0)</color>");
                return;
            }

            // Use AI profile list for names when available; fall back to aiInitializeDatas templates.
            List<AIProfile> profiles = null;
            if (aiProfileList != null)
                profiles = aiProfileList.PickRandom(aiCount);

            // Tournament: seed the AI roster once (first game) and reuse it for every later
            // game, so name-keyed bot standings attribute correctly across the lineup. The
            // names match profile names, so downstream avatar resolution still works.
            bool tournament = gameData.IsTournamentMode && tournamentData != null;
            if (tournament && tournamentData.TournamentAINames.Count == 0 && profiles != null)
            {
                for (int p = 0; p < profiles.Count; p++)
                    tournamentData.TournamentAINames.Add(profiles[p].Name);
            }

            // The whole loop runs synchronously in ONE frame — the dominant launch spike at
            // high player counts. The span makes that cost (and its scaling) visible.
            using var _ = LoadInsights.Measure(LoadInsightCategory.AiBackfill,
                $"AI backfill spawn loop ({aiCount} AI players+vessels, single frame)");

            for (int i = 0; i < aiCount; i++)
            {
                LoadInsights.Count("AI players spawned during load");
                var aiPlayerNO = Instantiate(aiPlayerPrefab);
                GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);

                // destroyWithScene=false - AI spawns in same tick as scene load; see ClearPlayerVesselReferences for cleanup.
                aiPlayerNO.Spawn(false);

                var aiPlayer = aiPlayerNO.GetComponent<Player>();
                if (!aiPlayer)
                {
                    CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] AI Player prefab missing Player component.");
                    aiPlayerNO.Despawn(true);
                    continue;
                }

                // Claim BEFORE the NetworkVariable writes below: they raise the deferred spawn
                // event, and this loop is only safe today because it runs before base.OnNetworkSpawn
                // subscribes. Claiming per-spawn makes that ordering non-load-bearing (the sweep
                // after the loop stays as the belt to this suspenders).
                ClaimExternallySpawnedPlayer(aiPlayer);

                // Use template data if available, otherwise derive values dynamically
                var hasTemplate = aiInitializeDatas != null && i < aiInitializeDatas.Length;

                var aiVesselType = hasTemplate ? aiInitializeDatas[i].vesselClass : VesselClassType.Random;
                if (aiVesselType is VesselClassType.Any or VesselClassType.Random)
                    aiVesselType = PickAIVesselType();

                // A restricted-vessel mode restricts the AI too. The AI's class comes from the
                // scene's aiInitializeDatas (or the captain roll), neither of which knows the
                // mode's rules - so a scene authored with the wrong template, or a captain roll
                // in a single-vessel mode, would field opponents in an illegal hull. Same clamp
                // and same authority as the human path (ResolveSpawnVesselType); no-op when the
                // game authors no Vessels list.
                aiVesselType = gameData.ClampVesselToGame(aiVesselType);

                var aiName = tournament && i < tournamentData.TournamentAINames.Count
                    ? tournamentData.TournamentAINames[i]
                    : profiles != null && i < profiles.Count
                        ? profiles[i].Name
                        : hasTemplate ? aiInitializeDatas[i].PlayerName : $"AI {i + 1}";

                // A domain the host PLACED (the launch panel's Add AI mode) wins - ALWAYS, even
                // past the DomainCount prefix: placing on Gold in a two-domain lobby is the host
                // widening the match, and re-balancing it away silently is exactly the "cannot
                // add to gold" playtest defect. Only Blue (unset) falls back to the balanced pick.
                // A placed domain is never written into a count dict that lacks its key:
                // GetBalancedDomain requires a domain in BOTH dicts, and a half-known key sets a
                // minTotal no listed domain can then match, starving the pick to its error path.
                Domains aiDomain;
                var placed = i < gameData.RequestedAIDomains.Count
                    ? gameData.RequestedAIDomains[i]
                    : Domains.Blue;
                if (placed != Domains.Blue)
                {
                    aiDomain = placed;
                    if (totalCounts.ContainsKey(aiDomain)) totalCounts[aiDomain]++;
                }
                else
                {
                    aiDomain = GetBalancedDomain(totalCounts, humanCounts);
                    totalCounts[aiDomain]++;
                }

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
        /// Picks the active domain for the next AI placement using a deterministic
        /// 3-tier tie-break:
        ///   1. Lowest total (humans + AI assigned so far).
        ///   2. Among ties, fewest humans (so AI fill solo-AI domains before
        ///      doubling up alongside a human).
        ///   3. Final tie-break is <see cref="GameDataSO.ActiveDomains"/> enum
        ///      order (Jade → Ruby → Gold).
        /// Identical inputs produce identical results on every machine - no shared
        /// RNG seed needed.
        /// </summary>
        public static Domains GetBalancedDomain(
            Dictionary<Domains, int> totalCounts,
            Dictionary<Domains, int> humanCounts)
        {
            int minTotal = int.MaxValue;
            foreach (var d in GameDataSO.ActiveDomains)
                if (totalCounts.TryGetValue(d, out var t) && t < minTotal)
                    minTotal = t;

            int minHumans = int.MaxValue;
            foreach (var d in GameDataSO.ActiveDomains)
                if (totalCounts.TryGetValue(d, out var t) && t == minTotal
                    && humanCounts.TryGetValue(d, out var h) && h < minHumans)
                    minHumans = h;

            foreach (var d in GameDataSO.ActiveDomains)
                if (totalCounts.TryGetValue(d, out var t) && t == minTotal
                    && humanCounts.TryGetValue(d, out var h) && h == minHumans)
                    return d;

            // Reachable only when totalCounts is empty (no active domains) -
            // degrade gracefully rather than throw.
            CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] GetBalancedDomain: empty counts");
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

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF00FF>[FLOW-5AI] GatherHumanPlayers: found {humans.Count} humans</color>");
            return humans;
        }

        /// <summary>
        /// Builds the active-domain set as the contiguous slice
        /// <see cref="GameDataSO.ActiveDomains"/>[0..DC-1] where DC is the
        /// requested domain count clamped to [1, ActiveDomains.Length]. DC < 3
        /// means lower-priority domains (Gold first, then Ruby) are unavailable.
        /// </summary>
        public static List<Domains> BuildActiveDomains(int requestedDomainCount)
        {
            int dc = Mathf.Clamp(requestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            var active = new List<Domains>(dc);
            for (int i = 0; i < dc; i++) active.Add(GameDataSO.ActiveDomains[i]);
            return active;
        }

        /// <summary>
        /// Reassigns any human whose <see cref="Player.NetDomain"/> is outside
        /// the active set onto a balanced active domain (same algorithm AI uses).
        /// Mutates both <paramref name="totalCounts"/> and <paramref name="humanCounts"/>
        /// so subsequent AI placement sees the rebalanced state.
        /// </summary>
        static void NormalizeUnassignedHumans(
            List<Player> humans,
            List<Domains> activeDomains,
            Dictionary<Domains, int> totalCounts,
            Dictionary<Domains, int> humanCounts)
        {
            int reassigned = 0;
            foreach (var h in humans)
            {
                var d = h.NetDomain.Value;
                if (totalCounts.ContainsKey(d)) continue;

                var assigned = GetBalancedDomain(totalCounts, humanCounts);
                totalCounts[assigned]++;
                humanCounts[assigned]++;
                h.NetDomain.Value = assigned;
                reassigned++;
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF00FF>[FLOW-5AI] NormalizeUnassignedHumans: assigned {h.NetName.Value} ({d}) → {assigned}</color>");
            }

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF00FF>[FLOW-5AI] NormalizeUnassignedHumans: {reassigned}/{humans.Count} humans reassigned, totals={string.Join(", ", totalCounts)}</color>");
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

            using (LoadInsights.Measure(LoadInsightCategory.AiBackfill, $"AI vessel instantiate+inject+spawn ({vesselType})"))
            {
                vesselNO = Instantiate(shipNetworkObject);
                GameObjectInjector.InjectRecursive(vesselNO.gameObject, _container);
                // destroyWithScene=false matches the AI player spawn - must stay consistent for cleanup ordering.
                vesselNO.Spawn(false);
                aiPlayer.NetVesselId.Value = vesselNO.NetworkObjectId;
                LoadInsights.Count("Vessels spawned during load");
                return true;
            }
        }

        void ConfigureAIPilot(NetworkObject aiVesselNO)
        {
            var aiPilot = aiVesselNO.GetComponentInChildren<AIPilot>();
            if (aiPilot == null) return;
            ConfigureAIPilotForMode(aiPilot, gameData);
        }

        /// <summary>
        /// The platform's own answer to "how should an AI pilot be set up for THIS match?"
        /// — the rule the backfill has always applied, lifted to a static so a second
        /// caller cannot end up with a hand-copied version of it that drifts.
        ///
        /// The AI training framework is that second caller: it flies the host's vessel on
        /// autopilot too, and a pilot configured differently from the ones it races is not
        /// an opponent, it is a confound.
        /// </summary>
        public static void ConfigureAIPilotForMode(AIPilot aiPilot, GameDataSO gameData)
        {
            if (aiPilot == null || gameData == null) return;

            // Player-seek is for the modes whose OBJECTIVE is another pilot. Joust wants to
            // sweep its skimmer past you; Dog Fight wants you in its gunsight - the steering
            // need is identical (chase the live position of a chosen opponent), so the mode
            // reuses AIPilot's existing opponent lock rather than growing a bespoke one. Dog
            // Fight then layers a stand-off distance on top via its own external target
            // provider, because a gun duel is not a ramming contest.
            bool shouldSeekPlayers =
                gameData.GameMode == GameModes.MultiplayerJoust ||
                gameData.GameMode == GameModes.DogFight;
            float intensity = gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 4;
            float skill = Mathf.Clamp01(intensity * 0.25f);
            aiPilot.ConfigureForGameMode(gameData, shouldSeekPlayers, skill);
        }
    }
}
