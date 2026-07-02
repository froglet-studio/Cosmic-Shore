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
using Random = CosmicShore.Engine.Random;

namespace CosmicShore.Cli
{
    /// <summary>Knobs for one headless AI-vs-AI Astro League match.</summary>
    public sealed class AstroLeagueRoundOptions
    {
        /// <summary>Total AI players (balanced Jade vs Ruby — Astro League is a two-domain mode).</summary>
        public int PlayerCount = 4;
        public int Seed = 42;

        /// <summary>Per-domain goal sum that mercy-rule ends the match (AstroLeagueSettingsSO.goalLimit).</summary>
        public int GoalLimit = 3;

        /// <summary>Regulation clock (seconds). Ties at full time go to golden-goal overtime.</summary>
        public float MatchDurationSeconds = 120f;

        /// <summary>Fail-loud frame cap (default 20 simulated minutes @ 60 Hz — covers overtime).</summary>
        public int MaxFrames = 60 * 60 * 20;

        public float DeltaTime = 1f / 60f;

        /// <summary>Radius of each vessel's trigger contact bubble (the trigger-only-ship strike path).</summary>
        public float VesselContactRadius = 10f;

        /// <summary>Goal-mouth radius override (the scene default is 26; wider converges faster headless).</summary>
        public float GoalMouthRadius = 60f;
    }

    /// <summary>One row of the final standings (derived from GameDataSO.Results + RoundStats).</summary>
    public sealed class AstroLeagueStanding
    {
        public int Rank;
        public string Name;
        public Domains Domain;
        public int Goals;
        public float Score;
        public string ScoreText;
    }

    public sealed class AstroLeagueRoundResult
    {
        public bool Finished;
        public string WinnerName = "";
        public Domains WinnerDomain = Domains.Blue;
        public int JadeGoals;
        public int RubyGoals;
        public int TotalStrikes;
        public bool WentToOvertime;
        public int FramesSimulated;

        /// <summary>Deterministic line-by-line log: kickoffs, goals, clock beats, finish, standings.</summary>
        public List<string> Transcript = new();

        public List<AstroLeagueStanding> Standings = new();

        /// <summary>Error/Exception entries captured from the engine log during the match (expected empty).</summary>
        public List<string> EngineErrors = new();
    }

    /// <summary>No-op HUD controller for the vessel fixture's serialized slot (headless).</summary>
    sealed class LeagueVesselHud : MonoBehaviour, IVesselHUDController
    {
        public void Initialize(IVesselStatus vesselStatus) { }
        public void SubscribeToEvents() { }
        public void UnsubscribeFromEvents() { }
        public void ShowHUD() { }
        public void HideHUD() { }
        public void SetBlockPrefab(GameObject prefab) { }
    }

    /// <summary>Concrete VesselAnimation (base is abstract) — no-op puppetry.</summary>
    sealed class LeagueVesselAnimation : Gameplay.VesselAnimation
    {
        protected override void AssignTransforms() { }
        protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle) { }
    }

    /// <summary>
    /// Headless AI-vs-AI Astro League match through the REAL controller chain:
    /// MiniGameControllerBase → MultiplayerMiniGameControllerBase →
    /// MultiplayerDomainGamesController → AstroLeagueController, all Spawn()ed host-mode
    /// (the scene-placed-NetworkBehaviour contract), driving the verbatim flow —
    /// InitializeAfterDelay → SetupNewRound/Turn → Ready → CountdownTimer → kickoff parking →
    /// live ball physics (engine E18 rigidbody integration + trigger-pass vessel strikes) →
    /// goal-plane detection (AstroLeagueGoal) → celebrations/kickoffs → mercy rule or full
    /// time (AstroLeagueMatchMonitor + TurnMonitorController) → golden goal →
    /// OnTurnEndedCustom → SyncFinalScores → GameDataSO.Results. The harness only
    /// constructs and wires (the scene + ServerPlayerVesselInitializer roles), observes
    /// SOAP events for the transcript, and tears everything down.
    /// </summary>
    public static class AstroLeagueRound
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

        static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        // ── round entry point ──────────────────────────────────────────────────

        public static AstroLeagueRoundResult Run(AstroLeagueRoundOptions options, Action<string> liveLog = null)
        {
            options ??= new AstroLeagueRoundOptions();
            int playerCount = Mathf.Clamp(options.PlayerCount, 2, 6);

            var result = new AstroLeagueRoundResult();
            void Log(string line)
            {
                result.Transcript.Add(line);
                liveLog?.Invoke(line);
            }

            var capturedLog = new CapturingLogSink();
            var previousSink = Debug.Sink;
            Debug.Sink = capturedLog;

            using var loop = new GameLoop("AstroLeagueRound");
            Random.InitState(options.Seed);
            // In-process test hygiene (HeadlessRoundTests pattern): a prior test's
            // PrismSpatialIndex singleton must not leak into the ball's per-tick scan.
            typeof(Singleton<PrismSpatialIndex>).GetProperty("Instance")!.SetValue(null, null);

            GameDataSO gameData = null;
            AstroLeagueController controller = null;
            AstroLeagueMatchMonitor matchMonitor = null;
            AstroLeagueBall ball = null;
            var spawnedBehaviours = new List<NetworkBehaviour>();
            var networkManagerGo = new GameObject("NetworkManager");
            NetworkManager.Singleton = networkManagerGo.AddComponent<NetworkManager>();

            try
            {
                // ── settings + rule (the SO assets of the real scene) ─────────
                var settings = ScriptableObject.CreateInstance<AstroLeagueSettingsSO>();
                settings.goalLimit = Mathf.Max(1, options.GoalLimit);
                settings.matchDurationSeconds = Mathf.Max(10f, options.MatchDurationSeconds);
                // Headless tuning (settings-asset values, the designer knob surface): the CLI's
                // Sparrow AI flies slower than a boosting human, so cap the ball's top speed at a
                // catchable pace — otherwise a full-power smack outruns the strikers and the match
                // devolves into chase-the-carom until overtime.
                settings.maxSpeed = 35f;
                settings.boundaryRadius = 170f;

                var rule = ScriptableObject.CreateInstance<AstroLeagueScoringRuleSO>();
                SetPrivateField(rule, "metric", ScoringMetric.Goals);
                SetPrivateField(rule, "golfRules", false);

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
                gameData.SceneName = "MinigameAstroLeague";
                gameData.GameMode = GameModes.AstroLeague;
                gameData.IsMultiplayerMode = true;
                gameData.RequestedDomainCount = 2; // Jade vs Ruby — pinned by the arcade card

                // ── AI course registry (empty — strikers run on the external provider) ──
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

                // ── the ball (Rigidbody + SphereCollider + AstroLeagueBall) ───
                var ballGo = new GameObject("AstroLeagueBall");
                ballGo.SetActive(false); // configure-before-Awake
                ballGo.transform.localScale = Vector3.one * 14f; // collider r=0.5 → world radius 7 (chunky billiard)
                ballGo.AddComponent<Rigidbody>();
                var ballCol = ballGo.AddComponent<SphereCollider>();
                ballCol.radius = 0.5f;
                ball = ballGo.AddComponent<AstroLeagueBall>();
                SetPrivateField(ball, "settings", settings);
                SetPrivateField(ball, "gameData", gameData);
                ballGo.SetActive(true); // Awake: rigidbody config + visuals
                ball.Spawn();           // host-mode NetworkBehaviour (server-authoritative sim)
                spawnedBehaviours.Add(ball);

                // ── arena + goals + team spawns (the scene layout) ────────────
                var arenaGo = new GameObject("AstroLeagueArena");
                arenaGo.SetActive(false);
                var arena = arenaGo.AddComponent<AstroLeagueArena>();
                SetPrivateField(arena, "settings", settings);
                SetPrivateField(arena, "ball", ball);
                arenaGo.SetActive(true);

                AstroLeagueGoal MakeGoal(string name, Domains defending, float z, AstroLeagueController owner)
                {
                    var go = new GameObject(name);
                    go.SetActive(false);
                    go.transform.position = new Vector3(0f, 0f, z);
                    var goal = go.AddComponent<AstroLeagueGoal>();
                    SetPrivateField(goal, "defendingDomain", defending);
                    SetPrivateField(goal, "mouthRadius", options.GoalMouthRadius);
                    SetPrivateField(goal, "controller", owner);
                    go.SetActive(true);
                    return goal;
                }

                Transform MakeSpawn(string name, float z)
                {
                    var go = new GameObject(name);
                    go.transform.position = new Vector3(0f, 0f, z);
                    // Face the arena center so anchor.right fans teammates laterally.
                    go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -Mathf.Sign(z)), Vector3.up);
                    return go.transform;
                }

                // ── the controller GO (Game object of the real scene) ─────────
                var controllerGo = new GameObject("Game");
                controllerGo.SetActive(false);
                var countdownTimer = controllerGo.AddComponent<CountdownTimer>();
                SetPrivateField(countdownTimer, "countdownDuration", 0.5f); // brisk CLI count-in (inspector value)
                matchMonitor = controllerGo.AddComponent<AstroLeagueMatchMonitor>();
                SetPrivateField(matchMonitor, "gameData", gameData);
                var displayChannel = ScriptableObject.CreateInstance<ScriptableEventString>();
                SetPrivateField(matchMonitor, "onUpdateTurnMonitorDisplay", displayChannel);
                var turnMonitorController = controllerGo.AddComponent<TurnMonitorController>();
                SetPrivateField(turnMonitorController, "gameData", gameData);
                SetPrivateField(turnMonitorController, "monitors", new List<TurnMonitor> { matchMonitor });

                controller = controllerGo.AddComponent<AstroLeagueController>();
                var goals = new List<AstroLeagueGoal>
                {
                    // Order must match GameDataSO.ActiveDomains (Jade, Ruby): Jade defends -Z, Ruby defends +Z.
                    MakeGoal("Goal_Jade", Domains.Jade, -150f, controller),
                    MakeGoal("Goal_Ruby", Domains.Ruby, +150f, controller),
                };
                var teamSpawns = new List<Transform>
                {
                    MakeSpawn("TeamSpawn_Jade", -settings.kickoffLineDistance),
                    MakeSpawn("TeamSpawn_Ruby", +settings.kickoffLineDistance),
                };
                SetPrivateField(controller, "settings", settings);
                SetPrivateField(controller, "rule", rule);
                SetPrivateField(controller, "ball", ball);
                SetPrivateField(controller, "arena", arena);
                SetPrivateField(controller, "matchMonitor", matchMonitor);
                SetPrivateField(controller, "goals", goals);
                SetPrivateField(controller, "teamSpawns", teamSpawns);
                SetPrivateField(controller, "countdownTimer", countdownTimer);
                var readyButtonChannel = ScriptableObject.CreateInstance<ScriptableEventBool>();
                SetPrivateField(controller, "_onToggleReadyButton", readyButtonChannel);
                container.InjectGameObject(controllerGo); // [Inject] gameData + audioSystem
                controllerGo.SetActive(true);

                // ── spawn the AI field (verbatim C6 pipeline, balanced Jade/Ruby) ──
                var vesselTemplate = BuildVesselPrefab("SparrowPrefab", VesselClassType.Sparrow, gameData,
                    courseData, sharedCellItemsEvent, options.VesselContactRadius);
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

                var spawnTransforms = new Transform[playerCount];
                for (int i = 0; i < playerCount; i++)
                {
                    var point = new GameObject($"spawnPoint{i}");
                    // Alternating team lines, fanned laterally — kickoff parking re-seats everyone anyway.
                    float side = i % 2 == 0 ? -1f : 1f;
                    point.transform.position = new Vector3((i / 2 - (playerCount / 4f)) * 40f, 0f, side * settings.kickoffLineDistance);
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

                    // Two-domain balance: even → Jade, odd → Ruby (2v2 / 3v3).
                    var domain = GameDataSO.ActiveDomains[i % 2];
                    ((Player)player).SetDomain(domain);
                    player.RoundStats.Domain = domain;

                    gameData.AddPlayer(player);

                    var vesselGo = ((VesselController)player.Vessel).gameObject;
                    vesselGo.SetActive(true);
                    player.StartPlayer(); // AI path: vessel un-stationed, AIPilot ON

                    // Known trap (C4/C6): end the async prism spawn loop so the process can exit
                    // (Astro League's prism game is the ball's scan — no trail mass needed headless).
                    ((VesselController)player.Vessel).VesselStatus.VesselPrismController.StopSpawn();

                    // ServerPlayerVesselInitializer role: register the vessel roster the ball's
                    // per-tick velocity sampler reads (network-spawn does this in the real scene).
                    gameData.Vessels.Add(player.Vessel);

                    players.Add(player);
                }

                // Single-process: the first AI doubles as the "local user" the ready flow reads
                // (rung-4 precedent — LocalPlayer/LocalRoundStats reflection-mirrored).
                SetProperty(gameData, "LocalPlayer", players[0]);
                SetProperty(gameData, "LocalRoundStats", players[0].RoundStats);

                // ── transcript observers (SOAP + controller events) ───────────
                bool matchEnded = false;
                bool readyShown = false;
                bool overtimeSeen = false;
                string lastClock = "";
                string lastLoggedStriker = null;

                gameData.OnMiniGameEnd.OnRaised += MarkEnded;
                void MarkEnded() => matchEnded = true;
                readyButtonChannel.OnRaised += OnReadyToggled;
                void OnReadyToggled(bool enabled) { if (enabled) readyShown = true; }

                // Known trap (C4/C6), kickoff edition: OnCountdownTimerEnded_ClientRpc →
                // SetPlayersActive → StartPlayer → StartVessel RE-ARMS every prism spawn loop.
                // The round wires no prism factory channel (Astro League's prism game is the
                // ball's spatial scan; headless there is no trail mass), so pause the spawner
                // again the moment the turn starts — pausing mass creation is allowed
                // (conserved-mass rules), aging it out would not be.
                gameData.OnMiniGameTurnStarted.OnRaised += StopAllPrismSpawns;
                void StopAllPrismSpawns()
                {
                    foreach (var p in players)
                        ((VesselController)p.Vessel).VesselStatus.VesselPrismController.StopSpawn();
                }

                displayChannel.OnRaised += OnClockDisplay;
                void OnClockDisplay(string display)
                {
                    if (display == lastClock) return;
                    lastClock = display;
                    if (display == "OT")
                    {
                        overtimeSeen = true;
                        Log($"[t={F(Time.time),7}s] ⏱ OVERTIME — golden goal, next score wins");
                    }
                    else if (display.EndsWith(":00") || display.EndsWith(":30"))
                    {
                        Log($"[t={F(Time.time),7}s] ⏱ clock {display} · Jade {SumGoals(Domains.Jade)} — {SumGoals(Domains.Ruby)} Ruby");
                    }
                }

                int SumGoals(Domains d) => ScoringMetrics.SumByDomain(gameData, ScoringMetric.Goals, d);

                ball.OnStruckServer += OnStruck;
                void OnStruck(IVessel vessel, float intensity)
                {
                    result.TotalStrikes++;
                    var striker = players.FirstOrDefault(p => ReferenceEquals(p.Vessel, vessel));
                    if (striker == null) return;
                    if (striker.Name == lastLoggedStriker) return; // log possession changes, not dribbles
                    lastLoggedStriker = striker.Name;
                    Log($"[t={F(Time.time),7}s] {striker.Name} ({striker.Domain}) strikes the ball (intensity {F(intensity)})");
                }

                var goalHandlers = new List<(IRoundStats stats, Action<IRoundStats> handler)>();
                var loggedGoals = new Dictionary<string, int>();
                foreach (var p in players)
                {
                    var captured = p;
                    Action<IRoundStats> handler = stats =>
                    {
                        // Log only genuine increments — SyncFinalScores_ClientRpc re-writes
                        // GoalsScored on every stat at match end, re-raising the event.
                        loggedGoals.TryGetValue(captured.Name, out int known);
                        if (stats.GoalsScored <= known) return;
                        loggedGoals[captured.Name] = stats.GoalsScored;

                        lastLoggedStriker = null;
                        Log($"[t={F(Time.time),7}s] ⚽ GOAL! {captured.Name} ({captured.Domain}) scores — " +
                            $"Jade {SumGoals(Domains.Jade)} — {SumGoals(Domains.Ruby)} Ruby");
                    };
                    p.RoundStats.OnGoalsScoredChanged += handler;
                    goalHandlers.Add((p.RoundStats, handler));
                }

                // ── kick the real flow: spawn the scene-placed NetworkBehaviours ──
                matchMonitor.Spawn();
                spawnedBehaviours.Add(matchMonitor);
                turnMonitorController.Spawn();
                spawnedBehaviours.Add(turnMonitorController);
                controller.Spawn(); // OnNetworkSpawn → config sync → InitializeAfterDelay → SetupNewRound/Turn
                spawnedBehaviours.Add(controller);

                Log($"match: {playerCount} AI ({(playerCount + 1) / 2}v{playerCount / 2}), goal limit {settings.goalLimit} (mercy), " +
                    $"clock {settings.matchDurationSeconds:0}s, golden goal {(settings.goldenGoalOvertime ? "on" : "off")}, seed {options.Seed}");

                // ── drive: wait for the Ready button, click it, then run the match ──
                int frames = 0;
                bool readyClicked = false;
                int diagEvery = Environment.GetEnvironmentVariable("ASTRO_DIAG") == "1" ? 600 : 0;
                while (frames < options.MaxFrames && !matchEnded)
                {
                    loop.Tick(options.DeltaTime);
                    frames++;

                    if (diagEvery > 0 && frames % diagEvery == 0)
                    {
                        var bp = ball.transform.position;
                        var bv = ball.Velocity;
                        string ai = string.Join(" | ", players.Select(p =>
                        {
                            var v = p.Vessel.Transform.position;
                            return $"{p.Name} d={F(Vector3.Distance(v, bp))}";
                        }));
                        Log($"[diag t={F(Time.time)}] ball ({F(bp.x)},{F(bp.y)},{F(bp.z)}) |v|={F(bv.magnitude)} frozen={ball.IsFrozen} | {ai}");
                    }

                    if (!readyClicked && readyShown)
                    {
                        readyClicked = true;
                        Log($"[t={F(Time.time),7}s] ready — count-in starts (kickoff parking at GO)");
                        controller.OnReadyClicked(); // DomainGames ready flow → countdown → kickoff
                    }
                }

                result.FramesSimulated = frames;
                result.WentToOvertime = overtimeSeen;

                // Detach observers before teardown ticks.
                gameData.OnMiniGameEnd.OnRaised -= MarkEnded;
                readyButtonChannel.OnRaised -= OnReadyToggled;
                gameData.OnMiniGameTurnStarted.OnRaised -= StopAllPrismSpawns;
                displayChannel.OnRaised -= OnClockDisplay;
                ball.OnStruckServer -= OnStruck;
                foreach (var (stats, handler) in goalHandlers)
                    stats.OnGoalsScoredChanged -= handler;

                if (!matchEnded)
                {
                    Log($"TIMEOUT — match did not finish within {frames} frames.");
                    return result;
                }

                // ── finish: read the shared end-game surface ──────────────────
                result.Finished = true;
                result.WinnerName = gameData.WinnerName;
                result.WinnerDomain = gameData.WinnerDomain;
                result.JadeGoals = SumGoals(Domains.Jade);
                result.RubyGoals = SumGoals(Domains.Ruby);

                Log("");
                Log($"FULL TIME — {result.WinnerDomain} wins {Math.Max(result.JadeGoals, result.RubyGoals)}–" +
                    $"{Math.Min(result.JadeGoals, result.RubyGoals)}{(overtimeSeen ? " (golden goal)" : "")} " +
                    $"after {frames} frames ({F(Time.time)}s).");
                Log($"WINNER    — {result.WinnerName} ({result.WinnerDomain}), top scorer of the winning domain.");
                Log("");
                Log("STANDINGS (points — higher is better):");

                var goalsByName = gameData.RoundStatsList.ToDictionary(s => (string)s.Name, s => s.GoalsScored);
                foreach (var row in gameData.Results)
                {
                    var standing = new AstroLeagueStanding
                    {
                        Rank = row.Rank,
                        Name = row.Name,
                        Domain = row.Domain,
                        Goals = goalsByName.TryGetValue(row.Name, out var g) ? g : 0,
                        Score = row.Score,
                        ScoreText = row.ScoreText,
                    };
                    result.Standings.Add(standing);
                    Log($"  #{standing.Rank} {standing.Name,-6} {standing.Domain,-5} {standing.Goals,2} goals  {standing.ScoreText}");
                }

                return result;
            }
            finally
            {
                // Wind down every async loop the chain started (async-void trap: nothing may
                // outlive the round) — despawn in reverse spawn order, stop the monitor, stop
                // AI/prism loops, then flush destroys.
                matchMonitor?.StopMonitor();
                for (int i = spawnedBehaviours.Count - 1; i >= 0; i--)
                    spawnedBehaviours[i].Despawn(); // controller despawn cancels matchCts + coroutines

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
                Time.timeScale = 1f; // solo hitstop/celebration slow-mo must not leak past the round
                Debug.Sink = previousSink;

                foreach (var entry in capturedLog.Entries)
                    if (entry.Type is LogType.Error or LogType.Exception)
                        result.EngineErrors.Add($"{entry.Type}: {entry.Message}");
            }
        }

        // ── vessel prefab fixture (HexRaceRound's builder, striker edition) ──

        /// <summary>
        /// Same programmatic vessel "prefab" as HexRaceRound.BuildVesselPrefab with one
        /// difference: the contact bubble is a TRIGGER SphereCollider (the trigger-only-ship
        /// shape — Serpent/Sparrow — whose strikes the ball receives through its verbatim
        /// OnTriggerEnter path). No crystal-side impactor rig: Astro League's contact game is
        /// entirely ball-side (AstroLeagueBall.HandleVesselTrigger → VesselContact).
        /// </summary>
        static GameObject BuildVesselPrefab(string name, VesselClassType vesselType, GameDataSO gameData,
            CellRuntimeDataSO cellData, ScriptableEventNoParam onCellItemsUpdated, float contactRadius)
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

            go.AddComponent<SilhouetteController>();

            var cameraCustomizer = go.AddComponent<VesselCameraCustomizer>();
            SetPrivateField(cameraCustomizer, "OnInitializePlayerCamera",
                ScriptableObject.CreateInstance<ScriptableEventTransform>());

            go.AddComponent<LeagueVesselAnimation>();

            var actionHandler = go.AddComponent<R_VesselActionHandler>();
            SetPrivateField(actionHandler, "_onButtonPressed", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_onButtonReleased", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>());

            var geometry = new GameObject("geometry");
            geometry.transform.SetParent(go.transform);
            var customization = go.AddComponent<VesselCustomization>();
            SetPrivateField(customization, "_shipGeometries", new List<GameObject> { geometry });

            go.AddComponent<R_ShipElementStatsHandler>();

            var hud = go.AddComponent<LeagueVesselHud>();
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
            SetPrivateField(status, "_nearFieldSkimmer", NewChildSkimmer("nearFieldSkimmer"));
            SetPrivateField(status, "_farFieldSkimmer", NewChildSkimmer("farFieldSkimmer"));

            // Trigger-only-ship contact bubble: pairs with the ball's non-trigger sphere in the
            // engine trigger pass → AstroLeagueBall.OnTriggerEnter (its verbatim Serpent/Sparrow path).
            var contactBubble = go.AddComponent<SphereCollider>();
            contactBubble.isTrigger = true;
            contactBubble.radius = contactRadius;

            return go;
        }
    }
}
