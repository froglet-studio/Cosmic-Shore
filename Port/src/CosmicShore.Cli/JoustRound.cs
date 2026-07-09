using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
// Engine's UGS placeholder also declares an IPlayer — alias the game's player interface.
using IPlayer = CosmicShore.Gameplay.IPlayer;
using Random = CosmicShore.Engine.Random;

namespace CosmicShore.Cli
{
    /// <summary>Knobs for one headless AI-vs-AI Joust match.</summary>
    public sealed class JoustRoundOptions
    {
        /// <summary>Total AI players (balanced Jade vs Ruby — Joust needs 2 domains, min 2 players).</summary>
        public int PlayerCount = 4;
        public int Seed = 42;

        /// <summary>
        /// Per-domain joust sum that ends the turn. Authored through the REAL tool surface:
        /// the harness registers an EndConditionOverrides asset at the Resources path the
        /// ported JoustCollisionTurnMonitor loads (Tools &gt; Cosmic Shore &gt; End Game Conditions).
        /// </summary>
        public int JoustTarget = 3;

        /// <summary>Fail-loud frame cap (default 10 simulated minutes @ 60 Hz).</summary>
        public int MaxFrames = 60 * 60 * 10;

        public float DeltaTime = 1f / 60f;

        /// <summary>Radius of each vessel's non-trigger contact-bubble SphereCollider (world units).</summary>
        public float VesselContactRadius = 4f;

        /// <summary>Radius of each vessel's near-field skimmer trigger sphere (world units).</summary>
        public float SkimmerRadius = 12f;
    }

    /// <summary>One row of the final standings (derived from GameDataSO.Results + RoundStats).</summary>
    public sealed class JoustStanding
    {
        public int Rank;
        public string Name;
        public Domains Domain;
        public int Jousts;
        public float Score;
        public string ScoreText;
        public string Secondary;
    }

    public sealed class JoustRoundResult
    {
        public bool Finished;
        public string WinnerName = "";
        public Domains WinnerDomain = Domains.Blue;
        public float FinishTime;
        public int FramesSimulated;
        public int TotalJousts;

        /// <summary>Deterministic line-by-line log: jousts, finish, standings. Same seed → identical list.</summary>
        public List<string> Transcript = new();

        public List<JoustStanding> Standings = new();

        /// <summary>Error/Exception entries captured from the engine log during the match (expected empty).</summary>
        public List<string> EngineErrors = new();
    }

    /// <summary>No-op HUD controller for the vessel fixture's serialized slot (headless).</summary>
    sealed class JoustVesselHud : MonoBehaviour, IVesselHUDController
    {
        public void Initialize(IVesselStatus vesselStatus) { }
        public void SubscribeToEvents() { }
        public void UnsubscribeFromEvents() { }
        public void ShowHUD() { }
        public void HideHUD() { }
        public void SetBlockPrefab(GameObject prefab) { }
    }

    /// <summary>Concrete VesselAnimation (base is abstract) — no-op puppetry.</summary>
    sealed class JoustVesselAnimation : Gameplay.VesselAnimation
    {
        protected override void AssignTransforms() { }
        protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle) { }
    }

    /// <summary>
    /// Headless AI-vs-AI Joust match through the REAL controller chain:
    /// MiniGameControllerBase → MultiplayerMiniGameControllerBase →
    /// MultiplayerDomainGamesController → MultiplayerJoustController, all Spawn()ed
    /// host-mode, driving the verbatim flow — InitializeAfterDelay → SetupNewRound/Turn →
    /// Ready → CountdownTimer → SetPlayersActive/StartTurn → player-seek AI (the real
    /// AIPilot seekPlayers path Joust uses) → vessel-vs-skimmer TRIGGER-PASS contacts
    /// dispatched through the genuine impact pipeline (VesselImpactor / SkimmerImpactor /
    /// ImpactCollider) into the verbatim VesselExplosionBySkimmerEffectSO (faster-vessel +
    /// opponent-domain + anti-spam checks) → OnJoustCollision SOAP event →
    /// StatsManager-shaped bookkeeping onto RoundStats.JoustCollisions →
    /// NetworkJoustCollisionTurnMonitor + TurnMonitorController end the turn when a
    /// domain's joust sum reaches the target (JoustScoringRuleSO.IsObjectiveReached) →
    /// OnTurnEndedCustom → SyncJoustResults → GameDataSO.Results. The joust target is
    /// authored through the real EndConditionOverrides asset (registered at its Resources
    /// path). The harness only constructs and wires (the scene + spawner roles), observes
    /// SOAP events for the transcript, and tears everything down.
    /// </summary>
    public static class JoustRound
    {
        // ── reflection stand-in for inspector wiring of serialized fields ─────

        static void SetPrivateField(object target, string field, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null) continue;
                f.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }

        static void SetProperty(object target, string property, object value)
        {
            var p = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(target.GetType().Name, property);
            p.SetValue(target, value);
        }

        /// <summary>Reset the EndConditionOverridesSO cached-instance static (tool-asset hygiene).</summary>
        static void ResetEndConditionOverridesCache() =>
            typeof(EndConditionOverridesSO)
                .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, null);

        static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        // ── round entry point ──────────────────────────────────────────────────

        public static JoustRoundResult Run(JoustRoundOptions options, Action<string> liveLog = null)
        {
            options ??= new JoustRoundOptions();
            int playerCount = Mathf.Clamp(options.PlayerCount, 2, 12); // Joust minimum: 2 players, 2 domains

            var result = new JoustRoundResult();
            void Log(string line)
            {
                result.Transcript.Add(line);
                liveLog?.Invoke(line);
            }

            var capturedLog = new CapturingLogSink();
            var previousSink = Debug.Sink;
            Debug.Sink = capturedLog;

            using var loop = new GameLoop("JoustRound");
            Random.InitState(options.Seed);

            GameDataSO gameData = null;
            MultiplayerJoustController controller = null;
            NetworkJoustCollisionTurnMonitor joustMonitor = null;
            var spawnedBehaviours = new List<NetworkBehaviour>();
            var networkManagerGo = new GameObject("NetworkManager");
            NetworkManager.Singleton = networkManagerGo.AddComponent<NetworkManager>();

            try
            {
                // ── the joust target through the REAL tool surface ────────────
                // JoustCollisionTurnMonitor.StartMonitor loads EndConditionOverridesSO from
                // Resources/EndConditionOverrides — register the tool asset the way the
                // editor window authors it (0 there would mean "default 3").
                ResetEndConditionOverridesCache();
                var endConditions = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
                endConditions.joustCount = Mathf.Max(1, options.JoustTarget);
                Resources.Register(EndConditionOverridesSO.ResourcePath, endConditions);

                // ── the mode's scoring rule (the SO asset of the real scene) ──
                var rule = ScriptableObject.CreateInstance<JoustScoringRuleSO>();
                SetPrivateField(rule, "metric", ScoringMetric.Jousts);
                SetPrivateField(rule, "golfRules", true);

                // ── shared data: theme + GameDataSO + SOAP events ─────────────
                var theme = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
                theme.TeamMaterialSets = new Dictionary<Domains, SO_MaterialSet>();
                foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Blue, Domains.Gold })
                {
                    var set = ScriptableObject.CreateInstance<SO_MaterialSet>();
                    set.ShipMaterial = new Material((Shader)null) { name = $"{domain}-ship" };
                    set.BlockSilhouettePrefab = new GameObject($"{domain}-silhouette");
                    theme.TeamMaterialSets[domain] = set;
                }

                gameData = ScriptableObject.CreateInstance<GameDataSO>();
                gameData.ThemeManagerData = theme;
                gameData.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnSessionStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnMiniGameRoundStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnMiniGameRoundEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnMiniGameTurnStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnMiniGameTurnEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnMiniGameEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnWinnerCalculated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnPlayerNetworkSpawnedUlong = ScriptableObject.CreateInstance<ScriptableEventUlong>();
                gameData.OnVesselNetworkSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
                gameData.selectedVesselClass.Value = VesselClassType.Sparrow;
                gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
                gameData.SelectedIntensity.Value = 1;
                gameData.SelectedPlayerCount = ScriptableObject.CreateInstance<IntVariable>();
                gameData.SelectedPlayerCount.Value = playerCount;
                gameData.SceneName = "MinigameJoust_Gameplay";
                gameData.GameMode = GameModes.MultiplayerJoust;
                gameData.IsMultiplayerMode = true;
                gameData.RequestedDomainCount = 2; // Joust floor: MinDomainsAllowed = 2 (opponent-based mode)

                // ── AI course registry (empty — jousters run on the player-seek path) ──
                var sharedCellItemsEvent = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                var courseData = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
                SetPrivateField(courseData, "gameData", gameData);
                courseData.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                courseData.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                courseData.OnCellItemsUpdated = sharedCellItemsEvent;
                courseData.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();

                // ── DI (the [Inject] surface of the controller chain) ─────────
                var container = new Container();
                container.RegisterValue(gameData);
                var playerDataService = new GameObject("PlayerDataService").AddComponent<PlayerDataService>();
                container.RegisterValue(playerDataService);
                var audioSystem = new GameObject("AudioSystem").AddComponent<AudioSystem>();
                container.RegisterValue(audioSystem);

                // ── the joust scoring effect (the SO wired into the skimmer container) ──
                var onJoustCollision = ScriptableObject.CreateInstance<ScriptableEventString>();
                var joustEffect = ScriptableObject.CreateInstance<VesselExplosionBySkimmerEffectSO>();
                SetPrivateField(joustEffect, "OnJoustCollision", onJoustCollision);
                var skimmerContainer = ScriptableObject.CreateInstance<SkimmerImpactorDataContainerSO>();
                SetPrivateField(skimmerContainer, "vesselSkimmerEffectsSO",
                    new VesselSkimmerEffectsSO[] { joustEffect });

                // ── the controller GO (Game object of the real scene) ─────────
                var controllerGo = new GameObject("Game");
                controllerGo.SetActive(false);
                var countdownTimer = controllerGo.AddComponent<CountdownTimer>();
                SetPrivateField(countdownTimer, "countdownDuration", 0.5f); // brisk CLI count-in (inspector value)
                // Scene transcription: the real scene wires an Image child as the countdown display.
                var countdownDisplay = new GameObject("CountdownDisplay", typeof(RectTransform))
                    .AddComponent<CosmicShore.Engine.UI.Image>();
                countdownDisplay.transform.SetParent(controllerGo.transform, false);
                SetPrivateField(countdownTimer, "countdownDisplay", countdownDisplay);
                joustMonitor = controllerGo.AddComponent<NetworkJoustCollisionTurnMonitor>();
                SetPrivateField(joustMonitor, "gameData", gameData);
                var displayChannel = ScriptableObject.CreateInstance<ScriptableEventString>();
                SetPrivateField(joustMonitor, "onUpdateTurnMonitorDisplay", displayChannel);
                var turnMonitorController = controllerGo.AddComponent<TurnMonitorController>();
                SetPrivateField(turnMonitorController, "gameData", gameData);
                SetPrivateField(turnMonitorController, "monitors", new List<TurnMonitor> { joustMonitor });

                controller = controllerGo.AddComponent<MultiplayerJoustController>();
                SetPrivateField(controller, "rule", rule);
                SetPrivateField(controller, "countdownTimer", countdownTimer);
                var readyButtonChannel = ScriptableObject.CreateInstance<ScriptableEventBool>();
                SetPrivateField(controller, "_onToggleReadyButton", readyButtonChannel);
                container.InjectGameObject(controllerGo); // [Inject] gameData
                controllerGo.SetActive(true);

                // ── spawn the AI field (verbatim C6 pipeline, balanced Jade/Ruby) ──
                var vesselTemplate = BuildVesselPrefab("SparrowPrefab", VesselClassType.Sparrow, gameData,
                    courseData, sharedCellItemsEvent, options.VesselContactRadius, options.SkimmerRadius,
                    skimmerContainer);
                var prefabContainer = ScriptableObject.CreateInstance<VesselPrefabContainer>();
                SetPrivateField(prefabContainer, "_shipPrefabs", new[] { vesselTemplate.transform });

                var playerPrefabGo = new GameObject("PlayerPrefab");
                var playerPrefab = playerPrefabGo.AddComponent<Player>();
                SetPrivateField(playerPrefab, "gameData", gameData);

                var spawnerGo = new GameObject("Spawners");
                var vesselSpawner = spawnerGo.AddComponent<VesselSpawner>();
                SetPrivateField(vesselSpawner, "vesselPrefabContainer", prefabContainer);
                var playerSpawner = spawnerGo.AddComponent<PlayerSpawner>();
                SetPrivateField(playerSpawner, "_playerPrefab", playerPrefab);
                SetPrivateField(playerSpawner, "vesselSpawner", vesselSpawner);
                container.InjectGameObject(spawnerGo);

                // Alternating team line at z=0, fanned laterally, facing +Z.
                var spawnTransforms = new Transform[playerCount];
                for (int i = 0; i < playerCount; i++)
                {
                    var point = new GameObject($"spawnPoint{i}");
                    point.transform.position = new Vector3((i - (playerCount - 1) * 0.5f) * 60f, 0f, 0f);
                    spawnTransforms[i] = point.transform;
                }
                gameData.SetSpawnPositions(spawnTransforms);

                var players = new List<IPlayer>(playerCount);
                for (int i = 0; i < playerCount; i++)
                {
                    var player = playerSpawner.SpawnPlayerAndShip(new IPlayer.InitializeData
                    {
                        vesselClass = VesselClassType.Sparrow,
                        PlayerName = $"AI-{i + 1}",
                        AvatarId = 0,
                        AllowSpawning = true,
                        IsAI = true,
                    });
                    if (player == null)
                        throw new InvalidOperationException($"PlayerSpawner failed to spawn AI player {i + 1}.");

                    // Two-domain balance: even → Jade, odd → Ruby (1v1 / 2v2 / …).
                    var domain = GameDataSO.ActiveDomains[i % 2];
                    ((Player)player).SetDomain(domain);
                    player.RoundStats.Domain = domain;

                    gameData.AddPlayer(player);

                    var vesselGo = ((VesselController)player.Vessel).gameObject;
                    vesselGo.SetActive(true);

                    // The ServerPlayerVesselInitializerWithAI role: configure the pilot for the
                    // mode — Joust is the player-seek mode (seekPlayers=true). A seeded skill
                    // spread maps to distinct throttles (the SO_AIProfileList role), which is
                    // what makes one vessel of a contact pair strictly faster — the verbatim
                    // effect's speed check awards the joust to the faster vessel's skimmer.
                    var pilot = ((VesselController)player.Vessel).VesselStatus.AIPilot;
                    // Seeded skill draw (not an index-linear spread — that would correlate
                    // skill with domain and pre-decide every match for the same side).
                    float skill = Random.Range(0f, 1f);
                    pilot.ConfigureForGameMode(gameData, shouldSeekPlayers: true, skill: skill);
                    // AIPilot.Initialize already latched `throttle = defaultThrottle` during
                    // SpawnPlayerAndShip (before this Configure call could set the skill), so
                    // re-latch the working throttle to the configured skill's value — the same
                    // value Initialize would have produced had the skill been serialized.
                    SetPrivateField(pilot, "throttle", Mathf.Lerp(0.35f, 1.0f, skill));

                    // Vessels stay parked until the countdown ends (SetPlayersActive) — the
                    // real flow; jousts must not score before the turn starts.
                    players.Add(player);
                }

                // Single-process: the first AI doubles as the "local user" the ready flow and
                // the monitor's domain-remaining readout use (rung-4 precedent).
                SetProperty(gameData, "LocalPlayer", players[0]);
                SetProperty(gameData, "LocalRoundStats", players[0].RoundStats);

                // ── transcript observers (SOAP + StatsManager-shaped bookkeeping) ──
                bool matchEnded = false;
                bool readyShown = false;
                float turnStart = 0f;

                gameData.OnMiniGameEnd.OnRaised += MarkEnded;
                void MarkEnded() => matchEnded = true;
                readyButtonChannel.OnRaised += OnReadyToggled;
                void OnReadyToggled(bool enabled) { if (enabled) readyShown = true; }

                // Known trap (C4/C6): OnCountdownTimerEnded_ClientRpc → SetPlayersActive →
                // StartPlayer → StartVessel arms every prism spawn loop. The round wires no
                // prism factory channel (Joust's contact game is skimmer-vs-vessel; headless
                // there is no trail mass), so pause the spawner the moment the turn starts —
                // pausing mass creation is allowed (conserved-mass rules), aging it out is not.
                gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
                void OnTurnStarted()
                {
                    turnStart = Time.time;
                    foreach (var p in players)
                        ((VesselController)p.Vessel).VesselStatus.VesselPrismController.StopSpawn();
                }

                // The StatsManager role (StatsManager.ExecuteJoustCollision): the verbatim
                // effect raises OnJoustCollision with the SCORING vessel's name (the faster
                // vessel whose skimmer swept the slower opponent); the stat lands on that
                // player's RoundStats, whose setter drives the network monitor + HUD events.
                onJoustCollision.OnRaised += HandleJoustCollision;
                void HandleJoustCollision(string joustPlayerName)
                {
                    if (!gameData.TryGetRoundStats(joustPlayerName, out var roundStats)) return;
                    roundStats.JoustCollisions++;
                    result.TotalJousts++;

                    Log($"[t={F(Time.time - turnStart),7}s] {joustPlayerName} ({roundStats.Domain}) scores a joust — " +
                        $"Jade {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Jade)} · " +
                        $"Ruby {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Ruby)}");
                }

                // ── kick the real flow: spawn the scene-placed NetworkBehaviours ──
                joustMonitor.Spawn();
                spawnedBehaviours.Add(joustMonitor);
                turnMonitorController.Spawn();
                spawnedBehaviours.Add(turnMonitorController);
                controller.Spawn(); // OnNetworkSpawn → config sync → InitializeAfterDelay → SetupNewRound/Turn
                spawnedBehaviours.Add(controller);

                Log($"match: {playerCount} AI ({(playerCount + 1) / 2}v{playerCount / 2}), first domain to " +
                    $"{endConditions.GetJoustCount()} jousts wins (golf: winner scores elapsed time), seed {options.Seed}");

                // ── drive: wait for the Ready button, click it, then run the match ──
                int frames = 0;
                bool readyClicked = false;
                int diagEvery = Environment.GetEnvironmentVariable("JOUST_DIAG") == "1" ? 600 : 0;
                while (frames < options.MaxFrames && !matchEnded)
                {
                    loop.Tick(options.DeltaTime); // contacts happen inside: trigger pass → impactor dispatch
                    frames++;

                    if (diagEvery > 0 && frames % diagEvery == 0)
                    {
                        string ai = string.Join(" | ", players.Select(p =>
                        {
                            var v = ((VesselController)p.Vessel).VesselStatus;
                            var pos = p.Vessel.Transform.position;
                            return $"{p.Name} v={F(v.Speed)} ({F(pos.x)},{F(pos.y)},{F(pos.z)})";
                        }));
                        Log($"[diag t={F(Time.time)}] {ai}");
                    }

                    if (!readyClicked && readyShown)
                    {
                        readyClicked = true;
                        Log($"[t={F(Time.time),7}s] ready — count-in starts (lances up at GO)");
                        controller.OnReadyClicked(); // DomainGames ready flow → countdown → StartTurn
                    }
                }

                result.FramesSimulated = frames;

                // Detach observers before teardown ticks.
                gameData.OnMiniGameEnd.OnRaised -= MarkEnded;
                readyButtonChannel.OnRaised -= OnReadyToggled;
                gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
                onJoustCollision.OnRaised -= HandleJoustCollision;

                if (!matchEnded)
                {
                    Log($"TIMEOUT — match did not finish within {frames} frames.");
                    return result;
                }

                // ── finish: read the shared end-game surface ──────────────────
                result.Finished = true;
                result.WinnerName = gameData.WinnerName;
                result.WinnerDomain = gameData.WinnerDomain;
                result.FinishTime = gameData.RoundStatsList
                    .Where(s => GolfScoreSentinels.IsFinishTime(s.Score))
                    .Select(s => s.Score)
                    .DefaultIfEmpty(0f)
                    .First();

                Log("");
                Log($"OBJECTIVE — {result.WinnerDomain} domain reached {gameData.JoustTargetCount} jousts " +
                    $"in {F(result.FinishTime)}s ({frames} frames).");
                Log($"WINNER    — {result.WinnerName} ({result.WinnerDomain}), representative of the winning domain.");
                Log("");
                Log("STANDINGS (golf rules — lower score is better; losers tie on the sentinel):");

                var joustsByName = gameData.RoundStatsList.ToDictionary(s => (string)s.Name, s => s.JoustCollisions);
                foreach (var row in gameData.Results)
                {
                    var standing = new JoustStanding
                    {
                        Rank = row.Rank,
                        Name = row.Name,
                        Domain = row.Domain,
                        Jousts = joustsByName.TryGetValue(row.Name, out var j) ? j : 0,
                        Score = row.Score,
                        ScoreText = row.ScoreText,
                        Secondary = row.Secondary,
                    };
                    result.Standings.Add(standing);
                    Log($"  #{standing.Rank} {standing.Name,-6} {standing.Domain,-5} {standing.Jousts,2} jousts  score={F(standing.Score),9}  {standing.ScoreText}");
                }

                return result;
            }
            finally
            {
                // Wind down every async loop the chain started (async-void trap: nothing may
                // outlive the round) — stop the monitor, despawn in reverse spawn order, stop
                // AI/prism loops, then flush destroys.
                joustMonitor?.StopMonitor();
                for (int i = spawnedBehaviours.Count - 1; i >= 0; i--)
                    spawnedBehaviours[i].Despawn();

                if (gameData != null)
                {
                    foreach (var p in gameData.Players.ToList())
                    {
                        if (p?.Vessel is VesselController vc)
                            vc.VesselStatus.VesselPrismController.StopSpawn();
                        p?.ResetForPlay(); // AI path: toggles the AIPilot OFF
                    }
                }
                loop.Tick(options.DeltaTime); // end-of-frame destroy flush

                typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
                typeof(AudioSystem).GetProperty("Instance")!.SetValue(null, null); // shell singleton (Awake-set)
                NetworkManager.Singleton = null;
                Resources.Register(EndConditionOverridesSO.ResourcePath, null); // unregister the tool asset
                ResetEndConditionOverridesCache();
                Debug.Sink = previousSink;

                foreach (var entry in capturedLog.Entries)
                    if (entry.Type is LogType.Error or LogType.Exception)
                        result.EngineErrors.Add($"{entry.Type}: {entry.Message}");
            }
        }

        // ── vessel prefab fixture (HexRaceRound's builder, jouster edition) ──

        /// <summary>
        /// Same programmatic vessel "prefab" as HexRaceRound.BuildVesselPrefab with the joust
        /// contact rig instead of the crystal rig: the vessel root keeps the non-trigger
        /// contact bubble + VesselImpactor (+ NetworkVesselImpactor pair) + ImpactCollider,
        /// and the near-field skimmer child gains a trigger SphereCollider + SkimmerImpactor
        /// (carrying the joust-effect container) + ImpactCollider. A vessel entering an
        /// opposing skimmer's trigger sphere dispatches through the genuine impact pipeline
        /// on both sides — the skimmer side runs VesselExplosionBySkimmerEffectSO (the joust
        /// scoring chain). The E16 clone remap rewrites the intra-prefab references per clone.
        /// </summary>
        static GameObject BuildVesselPrefab(string name, VesselClassType vesselType, GameDataSO gameData,
            CellRuntimeDataSO cellData, ScriptableEventNoParam onCellItemsUpdated, float contactRadius,
            float skimmerRadius, SkimmerImpactorDataContainerSO skimmerContainer)
        {
            var go = new GameObject(name);
            go.SetActive(false);

            go.AddComponent<VesselPrismController>();

            var resources = go.AddComponent<Gameplay.ResourceSystem>();
            resources.Resources = new List<Gameplay.Resource> { new() { Name = "Energy" } };

            go.AddComponent<VesselTransformer>();

            var pilot = go.AddComponent<AIPilot>();
            SetPrivateField(pilot, "cellData", cellData);
            SetPrivateField(pilot, "OnCellItemsUpdated", onCellItemsUpdated);
            SetPrivateField(pilot, "abilities", new List<AIAbility>());
            // Skill → throttle spread (the SO_AIProfileList inspector values): distinct
            // per-pilot speeds are what let the faster vessel's skimmer score jousts.
            SetPrivateField(pilot, "defaultThrottleLow", 0.35f);
            SetPrivateField(pilot, "defaultThrottleHigh", 1.0f);

            go.AddComponent<SilhouetteController>();

            var cameraCustomizer = go.AddComponent<VesselCameraCustomizer>();
            SetPrivateField(cameraCustomizer, "OnInitializePlayerCamera",
                ScriptableObject.CreateInstance<ScriptableEventTransform>());

            go.AddComponent<JoustVesselAnimation>();

            var actionHandler = go.AddComponent<R_VesselActionHandler>();
            SetPrivateField(actionHandler, "_onButtonPressed", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_onButtonReleased", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>());

            var geometry = new GameObject("geometry");
            geometry.transform.SetParent(go.transform);
            var customization = go.AddComponent<VesselCustomization>();
            SetPrivateField(customization, "_shipGeometries", new List<GameObject> { geometry });

            go.AddComponent<R_ShipElementStatsHandler>();

            var hud = go.AddComponent<JoustVesselHud>();
            var controller = go.AddComponent<VesselController>();
            SetPrivateField(controller, "gameData", gameData);
            var status = go.AddComponent<VesselStatus>();

            Skimmer NewChildSkimmer(string skimmerName)
            {
                var child = new GameObject(skimmerName);
                child.transform.SetParent(go.transform);
                var skimmer = child.AddComponent<Skimmer>();
                SetPrivateField(skimmer, "onSkimmerShipImpact", ScriptableObject.CreateInstance<ScriptableEventString>());
                return skimmer;
            }

            var orientationHandle = new GameObject("orientationHandle");
            orientationHandle.transform.SetParent(go.transform);

            SetPrivateField(status, "_shipInstance", controller);
            SetPrivateField(status, "vesselHUDController", hud);
            SetPrivateField(status, "orientationHandle", orientationHandle);
            SetPrivateField(status, "_name", name);
            SetPrivateField(status, "vesselType", vesselType);
            var nearFieldSkimmer = NewChildSkimmer("nearFieldSkimmer");
            SetPrivateField(status, "_nearFieldSkimmer", nearFieldSkimmer);
            SetPrivateField(status, "_farFieldSkimmer", NewChildSkimmer("farFieldSkimmer"));

            // Joust contact rig, vessel side: non-trigger contact bubble + impactor routing
            // (identical to the HexRace crystal rig — the vessel-side half of every contact).
            var contactBubble = go.AddComponent<SphereCollider>();
            contactBubble.radius = contactRadius;

            var networkVesselImpactor = go.AddComponent<NetworkVesselImpactor>();
            var vesselImpactor = go.AddComponent<VesselImpactor>();
            SetPrivateField(vesselImpactor, "vesselImpactorDataContainerSO",
                ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>());
            SetPrivateField(vesselImpactor, "networkVesselImpactor", networkVesselImpactor);
            SetPrivateField(networkVesselImpactor, "vesselImpactor", vesselImpactor);

            var vesselImpactCollider = go.AddComponent<ImpactCollider>();
            SetPrivateField(vesselImpactCollider, "impactorObject", vesselImpactor);

            // Joust contact rig, skimmer side: the near-field skimmer child carries the
            // trigger sphere + SkimmerImpactor with the joust-effect container (the
            // SquirrelSkimmerImpactorDataContainer role) + ImpactCollider.
            var skimmerGo = nearFieldSkimmer.gameObject;
            var skimmerTrigger = skimmerGo.AddComponent<SphereCollider>();
            skimmerTrigger.isTrigger = true;
            skimmerTrigger.radius = skimmerRadius;

            var skimmerImpactor = skimmerGo.AddComponent<SkimmerImpactor>();
            SetPrivateField(skimmerImpactor, "skimmer", nearFieldSkimmer);
            SetPrivateField(skimmerImpactor, "skimmerImpactorDataContainer", skimmerContainer);

            var skimmerImpactCollider = skimmerGo.AddComponent<ImpactCollider>();
            SetPrivateField(skimmerImpactCollider, "impactorObject", skimmerImpactor);

            return go;
        }
    }
}
