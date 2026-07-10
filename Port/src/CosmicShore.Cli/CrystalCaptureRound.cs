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
// The engine's UGS session placeholder also declares an IPlayer (Unity.Services.Multiplayer
// contract) — alias the game's player interface, which is what this harness means.
using IPlayer = CosmicShore.Gameplay.IPlayer;
using Object = CosmicShore.Engine.Object;
using Random = CosmicShore.Engine.Random;

namespace CosmicShore.Cli
{
    /// <summary>Knobs for one headless AI-vs-AI Crystal Capture round.</summary>
    public sealed class CrystalCaptureRoundOptions
    {
        /// <summary>Total AI players (Crystal Capture supports 1-4; domains balance Jade→Ruby→Gold).</summary>
        public int PlayerCount = 4;
        public int Seed = 42;

        /// <summary>
        /// Per-domain crystal sum that ends the turn. Authored through the REAL tool surface:
        /// the harness registers an EndConditionOverrides asset at the Resources path the
        /// ported CrystalCollisionTurnMonitor loads (Tools &gt; Cosmic Shore &gt; End Game Conditions).
        /// </summary>
        public int CrystalTarget = 10;

        /// <summary>Fail-loud frame cap (default 10 simulated minutes @ 60 Hz).</summary>
        public int MaxFrames = 60 * 60 * 10;

        public float DeltaTime = 1f / 60f;

        /// <summary>Center-to-center claim distance (crystal trigger radius + vessel bubble radius).</summary>
        public float ClaimRadius = 25f;

        /// <summary>Radius of the vessel's contact-bubble SphereCollider (world units).</summary>
        public float VesselContactRadius = 4f;
    }

    /// <summary>One row of the final standings (derived from GameDataSO.Results + RoundStats).</summary>
    public sealed class CrystalCaptureStanding
    {
        public int Rank;
        public string Name;
        public Domains Domain;
        public int Crystals;
        public float Score;
        public string ScoreText;
    }

    public sealed class CrystalCaptureRoundResult
    {
        public bool Finished;
        public string WinnerName = "";
        public Domains WinnerDomain = Domains.Blue;
        public int WinnerDomainCrystals;
        public int FramesSimulated;
        public int TotalClaims;

        /// <summary>Deterministic line-by-line log: claims, finish, standings. Same seed → identical list.</summary>
        public List<string> Transcript = new();

        public List<CrystalCaptureStanding> Standings = new();

        /// <summary>Error/Exception entries captured from the engine log during the round (expected empty).</summary>
        public List<string> EngineErrors = new();
    }

    /// <summary>No-op HUD controller for the vessel fixture's serialized slot (headless).</summary>
    sealed class CaptureVesselHud : MonoBehaviour, IVesselHUDController
    {
        public void Initialize(IVesselStatus vesselStatus) { }
        public void SubscribeToEvents() { }
        public void UnsubscribeFromEvents() { }
        public void ShowHUD() { }
        public void HideHUD() { }
        public void SetBlockPrefab(GameObject prefab) { }
    }

    /// <summary>Concrete VesselAnimation (base is abstract) — no-op puppetry.</summary>
    sealed class CaptureVesselAnimation : Gameplay.VesselAnimation
    {
        protected override void AssignTransforms() { }
        protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle) { }
    }

    /// <summary>
    /// Headless AI-vs-AI Crystal Capture round through the REAL controller chain:
    /// MiniGameControllerBase → MultiplayerMiniGameControllerBase →
    /// MultiplayerDomainGamesController → MultiplayerCrystalCaptureController, all
    /// Spawn()ed host-mode, driving the verbatim flow — InitializeAfterDelay →
    /// SetupNewRound/Turn → Ready → CountdownTimer → SetPlayersActive/StartTurn →
    /// crystal-seek AI (AIPilot ← CellRuntimeDataSO.OnCellItemsUpdated) → crystal claims
    /// through the genuine trigger-pass impact pipeline (HexRaceRound's contact rig) onto
    /// RoundStats.CrystalsCollected → NetworkCrystalCollisionTurnMonitor +
    /// TurnMonitorController end the turn when a domain's crystal sum reaches the target
    /// (CrystalCaptureScoringRuleSO.IsObjectiveReached, target published through the
    /// monitor's real EndConditionOverrides → gameData.CrystalTargetCount path) →
    /// OnTurnEndedCustom → SyncFinalScores → GameDataSO.Results (points, not golf).
    /// The harness only constructs and wires, applies the StatsManager-shaped claim
    /// bookkeeping, observes SOAP events for the transcript, and tears everything down.
    /// </summary>
    /// <summary>
    /// A LIVE Crystal Capture round the caller steps one engine frame at a time
    /// (Arc G: the windowed mode host drives this from its render loop; the CLI's
    /// blocking <see cref="CrystalCaptureRound.Run"/> is the same handle stepped in a
    /// while-loop). Owns the round's GameLoop, controller chain, and every fixture
    /// <see cref="CrystalCaptureRound.Setup"/> built; <see cref="Dispose"/> performs
    /// the exact teardown the CLI's finally block always did (monitor stop, reverse
    /// despawn, AI/prism wind-down, cell unregister, destroy flush, singleton +
    /// tool-asset resets, sink restore + EngineErrors flush) and then disposes the loop.
    /// </summary>
    public sealed class CrystalCaptureRoundHandle : IRoundDriver
    {
        internal CrystalCaptureRoundOptions options;
        internal Action<string> liveLog;
        internal CrystalCaptureRoundResult result;

        internal GameLoop loop;
        internal CapturingLogSink capturedLog;
        internal ILogSink previousSink;

        internal GameDataSO gameData;
        internal MultiplayerCrystalCaptureController controller;
        internal NetworkCrystalCollisionTurnMonitor crystalMonitor;
        internal GameObject cellHost;
        internal readonly List<NetworkBehaviour> spawnedBehaviours = new();
        internal CellRuntimeDataSO courseData;
        internal CrystalManager crystalManager;
        internal CrystalCaptureScoringRuleSO rule;
        internal List<IPlayer> players;
        internal ScriptableEventBool readyButtonChannel;

        internal Vector3[] coursePositions;
        internal Element[] courseElements;
        internal int courseIndex;
        internal Crystal activeCrystal;
        internal ScriptableEventCrystalStats onCrystalCollected;
        internal float crystalTriggerRadius;

        internal int target;
        internal int frames;
        internal bool roundEnded;
        internal bool readyShown;
        internal bool readyClicked;
        internal bool turnStarted;
        internal float turnStart;

        bool _steppingCompleted;
        bool _finished;
        bool _disposed;

        // ── IRoundDriver (world view for a rendering host) ───────────────────
        public string GameLabel => "CRYSTAL CAPTURE";
        public string ScoringLabel => "points - higher is better";
        public CrystalCaptureRoundOptions Options => options;
        public CrystalCaptureRoundResult Result => result;
        public GameDataSO GameData => gameData;
        public IReadOnlyList<IPlayer> Players => players;
        public Crystal ActiveCrystal => activeCrystal;
        public Vector3[] Course => coursePositions;
        public Element[] CourseElements => courseElements;
        public int CourseIndex => courseIndex;
        public int Target => target;
        public int FramesStepped => frames;
        public int MaxFrames => options.MaxFrames;
        public bool Live => turnStarted;
        public float ClockStart => turnStart;
        public bool Finished => result.Finished;
        public string WinnerName => result.WinnerName;
        public Domains WinnerDomain => result.WinnerDomain;
        public int TotalClaims => result.TotalClaims;
        public IEnumerable<(int Rank, string Name, Domains Domain, int Crystals, string ScoreText)> StandingRows
        {
            get
            {
                foreach (var s in result.Standings)
                    yield return (s.Rank, s.Name, s.Domain, s.Crystals, s.ScoreText);
            }
        }
        public int DomainScore(Domains domain) => ScoringMetrics.SumByDomain(gameData, rule.Metric, domain);
        public int PlayerScore(IPlayer player) => player.RoundStats.CrystalsCollected;
        public bool AutoReady { get; set; } = true;
        public bool ReadyPending => readyShown && !readyClicked;

        /// <summary>The Ready press (factored from StepFrame's auto-click; idempotent).</summary>
        public void ClickReady()
        {
            if (readyClicked || !readyShown) return;
            readyClicked = true;
            Log($"[t={CrystalCaptureRound.F(Time.time),7}s] ready — count-in starts (crystals seed at GO)");
            controller.OnReadyClicked(); // DomainGames ready flow → countdown → StartTurn
        }

        internal void Log(string line)
        {
            result.Transcript.Add(line);
            liveLog?.Invoke(line);
        }

        /// <summary>
        /// One engine frame: tick (the controller chain runs inside — ready flow,
        /// countdown, turn start, trigger-pass claims, turn monitor), then the CLI's
        /// ready-click follow-up. Returns true the frame the round ends
        /// (OnMiniGameEnd raised by the real controller).
        /// </summary>
        public bool StepFrame()
        {
            loop.Tick(options.DeltaTime);
            frames++;

            if (AutoReady)
                ClickReady();

            return roundEnded;
        }

        /// <summary>
        /// Ends the stepping phase (idempotent): stamps FramesSimulated and detaches
        /// the four observers — exactly what the CLI loop did after its while-loop.
        /// </summary>
        public void CompleteStepping()
        {
            if (_steppingCompleted) return;
            _steppingCompleted = true;

            result.FramesSimulated = frames;

            gameData.OnMiniGameEnd.OnRaised -= MarkEnded;
            readyButtonChannel.OnRaised -= OnReadyToggled;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            onCrystalCollected.OnRaised -= HandleCrystalCollected;
        }

        /// <summary>
        /// Finish: read the shared end-game surface the REAL controller published
        /// (WinnerName/Domain via SyncFinalScores, Results via SetResults) and log the
        /// standings. No-op unless the round ended.
        /// </summary>
        public void FinishAndScore()
        {
            CompleteStepping();
            if (_finished || !roundEnded) return;
            _finished = true;

            result.Finished = true;
            result.WinnerName = gameData.WinnerName;
            result.WinnerDomain = gameData.WinnerDomain;
            result.WinnerDomainCrystals = ScoringMetrics.SumByDomain(gameData, rule.Metric, result.WinnerDomain);

            Log("");
            Log($"OBJECTIVE — {result.WinnerDomain} domain captured {result.WinnerDomainCrystals} crystals " +
                $"in {CrystalCaptureRound.F(Time.time - turnStart)}s ({frames} frames).");
            Log($"WINNER    — {result.WinnerName} ({result.WinnerDomain}), best contributor on the winning domain.");
            Log("");
            Log("STANDINGS (points — higher is better; score = crystals captured):");

            var crystalsByName = gameData.RoundStatsList.ToDictionary(s => (string)s.Name, s => s.CrystalsCollected);
            foreach (var row in gameData.Results)
            {
                var standing = new CrystalCaptureStanding
                {
                    Rank = row.Rank,
                    Name = row.Name,
                    Domain = row.Domain,
                    Crystals = crystalsByName.TryGetValue(row.Name, out var c) ? c : 0,
                    Score = row.Score,
                    ScoreText = row.ScoreText,
                };
                result.Standings.Add(standing);
                Log($"  #{standing.Rank} {standing.Name,-6} {standing.Domain,-5} {standing.Crystals,2} crystals  {standing.ScoreText}");
            }
        }

        // ── observers (the CLI's local functions, promoted to handle methods) ──

        internal void MarkEnded() => roundEnded = true;

        internal void OnReadyToggled(bool enabled) { if (enabled) readyShown = true; }

        // Turn start: pause the (unwired) prism spawn loops (C4/C6 trap — pausing
        // mass creation is allowed, aging it out is not) and stage the first crystal.
        internal void OnTurnStarted()
        {
            if (turnStarted) return;
            turnStarted = true;
            turnStart = Time.time;
            foreach (var p in players)
                ((VesselController)p.Vessel).VesselStatus.VesselPrismController.StopSpawn();

            activeCrystal = CrystalCaptureRound.SpawnCrystal(courseData, crystalManager, coursePositions, courseIndex,
                courseElements[courseIndex], crystalTriggerRadius, onCrystalCollected);
        }

        // Claim observer — fires from INSIDE the trigger pass, at the end of the
        // genuine OnTriggerEnter → ImpactorBase.AcceptImpactee → ExecuteEffect chain
        // on the crystal's OmniCrystalImpactor. The harness applies the
        // StatsManager-shaped bookkeeping (RoundStats + elemental progression) and
        // stages the next waypoint crystal until the objective lands.
        internal void HandleCrystalCollected(CrystalStats stats)
        {
            result.TotalClaims++;
            var claimant = players.First(p => p.Name == stats.PlayerName);
            CrystalCaptureRound.ApplyCrystalPickup(claimant, stats.Element);

            Log($"[t={CrystalCaptureRound.F(Time.time - turnStart),7}s] {claimant.Name} ({claimant.Domain}) captures crystal #{courseIndex + 1} [{stats.Element}] — " +
                $"Jade {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Jade)} · " +
                $"Ruby {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Ruby)} · " +
                $"Gold {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Gold)}");

            // Stage the next waypoint crystal unless the round just ended.
            if (!rule.IsObjectiveReached(gameData, out _) && courseIndex + 1 < coursePositions.Length)
            {
                courseIndex++;
                activeCrystal = CrystalCaptureRound.SpawnCrystal(courseData, crystalManager, coursePositions, courseIndex,
                    courseElements[courseIndex], crystalTriggerRadius, onCrystalCollected);
            }
            else
            {
                activeCrystal = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Wind down every async loop the chain started (async-void trap: nothing may
            // outlive the round) — stop the monitor, despawn in reverse spawn order, stop
            // AI/prism loops, then flush destroys.
            crystalMonitor?.StopMonitor();
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
            if (cellHost != null)
                cellHost.SetActive(false); // unregisters Cell.ActiveCells + resets the course registry
            loop?.Tick(options.DeltaTime); // end-of-frame destroy flush

            typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
            typeof(AudioSystem).GetProperty("Instance")!.SetValue(null, null); // shell singleton (Awake-set)
            NetworkManager.Singleton = null;
            Resources.Register(EndConditionOverridesSO.ResourcePath, null); // unregister the tool asset
            CrystalCaptureRound.ResetEndConditionOverridesCache();
            Debug.Sink = previousSink;

            foreach (var entry in capturedLog.Entries)
                if (entry.Type is LogType.Error or LogType.Exception)
                    result.EngineErrors.Add($"{entry.Type}: {entry.Message}");

            loop?.Dispose();
        }
    }

    public static class CrystalCaptureRound
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
        internal static void ResetEndConditionOverridesCache() =>
            typeof(EndConditionOverridesSO)
                .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, null);

        internal static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        // ── round entry points ─────────────────────────────────────────────────

        /// <summary>The CLI's blocking round: Setup → step to end/timeout → read scores.</summary>
        public static CrystalCaptureRoundResult Run(CrystalCaptureRoundOptions options, Action<string> liveLog = null)
        {
            using var handle = Setup(options, liveLog);

            while (handle.frames < handle.options.MaxFrames && !handle.roundEnded)
            {
                if (handle.StepFrame())
                    break;
            }

            handle.CompleteStepping();

            if (!handle.roundEnded)
            {
                handle.Log($"TIMEOUT — no domain reached {handle.target} crystals within {handle.frames} frames.");
                return handle.result;
            }

            handle.FinishAndScore();
            return handle.result;
        }

        /// <summary>
        /// Builds the full round world — the REAL controller chain (controller GO with
        /// CountdownTimer + NetworkCrystalCollisionTurnMonitor + TurnMonitorController +
        /// MultiplayerCrystalCaptureController, Spawn()ed host-mode), the cell + course
        /// registry, the AI field, and the transcript observers — everything the CLI
        /// round did before its frame loop. The caller owns the returned handle.
        /// </summary>
        public static CrystalCaptureRoundHandle Setup(CrystalCaptureRoundOptions options, Action<string> liveLog = null)
        {
            options ??= new CrystalCaptureRoundOptions();
            int playerCount = Mathf.Clamp(options.PlayerCount, 1, 4);
            int target = Mathf.Max(1, options.CrystalTarget);

            var handle = new CrystalCaptureRoundHandle
            {
                options = options,
                liveLog = liveLog,
                result = new CrystalCaptureRoundResult(),
                target = target,
            };

            handle.capturedLog = new CapturingLogSink();
            handle.previousSink = Debug.Sink;
            Debug.Sink = handle.capturedLog;

            handle.loop = new GameLoop("CrystalCaptureRound");
            Random.InitState(options.Seed);

            var networkManagerGo = new GameObject("NetworkManager");
            NetworkManager.Singleton = networkManagerGo.AddComponent<NetworkManager>();

            try
            {
                // ── the crystal target through the REAL tool surface ──────────
                // NetworkCrystalCollisionTurnMonitor.StartMonitor resolves the target from
                // EndConditionOverridesSO (keyed by GameMode) and publishes it to
                // gameData.CrystalTargetCount, which the scoring rule reads.
                ResetEndConditionOverridesCache();
                var endConditions = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
                endConditions.crystalCaptureCrystalCount = target;
                Resources.Register(EndConditionOverridesSO.ResourcePath, endConditions);

                // ── the mode's scoring rule (the SO asset of the real scene) ──
                var rule = ScriptableObject.CreateInstance<CrystalCaptureScoringRuleSO>();
                handle.rule = rule;
                SetPrivateField(rule, "metric", ScoringMetric.Crystals);
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

                var gameData = ScriptableObject.CreateInstance<GameDataSO>();
                handle.gameData = gameData;
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
                gameData.SceneName = "MinigameCrystalCaptureMultiplayer_Gameplay";
                gameData.GameMode = GameModes.MultiplayerCrystalCapture;
                gameData.IsMultiplayerMode = true;
                gameData.RequestedDomainCount = Mathf.Min(3, playerCount);

                // ── the cell + course registry (V11/V12, HexRaceRound pattern) ──
                var sharedCellItemsEvent = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                var courseData = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
                handle.courseData = courseData;
                SetPrivateField(courseData, "gameData", gameData);
                courseData.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                courseData.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                courseData.OnCellItemsUpdated = sharedCellItemsEvent;
                courseData.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();

                // Crystal lifecycle manager — the real Crystal → CrystalManager chain.
                var crystalManagerGo = new GameObject("crystalcapture-crystal-manager");
                crystalManagerGo.SetActive(false); // configure-before-activation
                var crystalManager = crystalManagerGo.AddComponent<HexRaceCrystalManager>();
                handle.crystalManager = crystalManager;
                SetPrivateField(crystalManager, "cellData", courseData);
                crystalManagerGo.SetActive(true);

                var cellConfig = ScriptableObject.CreateInstance<CellConfigDataSO>();
                cellConfig.CellName = "CrystalCaptureCell";
                cellConfig.SenseRadiusOverride = 300f;

                handle.cellHost = new GameObject("crystalcapture-cell");
                handle.cellHost.SetActive(false);
                var cell = handle.cellHost.AddComponent<Cell>();
                cell.ID = 1;
                SetPrivateField(cell, "runtime", courseData);
                SetPrivateField(cell, "gameData", gameData);
                SetPrivateField(cell, "CellConfigs", new List<CellConfigDataSO> { cellConfig });
                handle.cellHost.SetActive(true); // Cell.Initialize binds on the controller's InitializeGame raise

                // ── DI (the [Inject] surface of the controller chain) ─────────
                var container = new Container();
                container.RegisterValue(gameData);
                var playerDataService = new GameObject("PlayerDataService").AddComponent<PlayerDataService>();
                container.RegisterValue(playerDataService);
                var audioSystem = AudioSystemRig.Create();
                container.RegisterValue(audioSystem);

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
                var crystalMonitor = controllerGo.AddComponent<NetworkCrystalCollisionTurnMonitor>();
                handle.crystalMonitor = crystalMonitor;
                SetPrivateField(crystalMonitor, "gameData", gameData);
                var displayChannel = ScriptableObject.CreateInstance<ScriptableEventString>();
                SetPrivateField(crystalMonitor, "onUpdateTurnMonitorDisplay", displayChannel);
                var turnMonitorController = controllerGo.AddComponent<TurnMonitorController>();
                SetPrivateField(turnMonitorController, "gameData", gameData);
                SetPrivateField(turnMonitorController, "monitors", new List<TurnMonitor> { crystalMonitor });

                var controller = controllerGo.AddComponent<MultiplayerCrystalCaptureController>();
                handle.controller = controller;
                SetPrivateField(controller, "rule", rule);
                SetPrivateField(controller, "countdownTimer", countdownTimer);
                var readyButtonChannel = ScriptableObject.CreateInstance<ScriptableEventBool>();
                handle.readyButtonChannel = readyButtonChannel;
                SetPrivateField(controller, "_onToggleReadyButton", readyButtonChannel);
                container.InjectGameObject(controllerGo); // [Inject] gameData
                controllerGo.SetActive(true);

                // ── seeded crystal course (HexRaceRound pattern) ──────────────
                int courseLength = target * 3; // max claims before some domain reaches the target
                handle.coursePositions = GenerateCourse(courseLength);
                handle.courseElements = new Element[courseLength];
                for (int i = 0; i < courseLength; i++)
                    handle.courseElements[i] = RollElement(i);

                // ── prefabs + spawners (verbatim C6 pipeline) ─────────────────
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

                // Flat spawn line at z=0 facing +Z (the course starts down +Z).
                var spawnTransforms = new Transform[playerCount];
                for (int i = 0; i < playerCount; i++)
                {
                    var point = new GameObject($"spawnPoint{i}");
                    point.transform.position = new Vector3((i - (playerCount - 1) * 0.5f) * 40f, 0f, 0f);
                    spawnTransforms[i] = point.transform;
                }
                gameData.SetSpawnPositions(spawnTransforms);

                // ── spawn the AI field through PlayerSpawner.SpawnPlayerAndShip ──
                handle.players = new List<IPlayer>(playerCount);
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

                    // Balanced domains over GameDataSO.ActiveDomains — same Jade→Ruby→Gold
                    // tie-break order ServerPlayerVesselInitializerWithAI.GetBalancedDomain uses.
                    var domain = GameDataSO.ActiveDomains[i % GameDataSO.ActiveDomains.Length];
                    ((Player)player).SetDomain(domain);
                    player.RoundStats.Domain = domain;

                    gameData.AddPlayer(player); // registers RoundStats + assigns a seeded spawn pose

                    var vesselGo = ((VesselController)player.Vessel).gameObject;
                    vesselGo.SetActive(true);

                    // Vessels stay parked until the countdown ends (SetPlayersActive) — the
                    // real flow; claims must not land before the turn starts.
                    handle.players.Add(player);
                }

                // Single-process: the first AI doubles as the "local user" the ready flow and
                // the monitor's crystals-remaining readout use (rung-4 precedent).
                SetProperty(gameData, "LocalPlayer", handle.players[0]);
                SetProperty(gameData, "LocalRoundStats", handle.players[0].RoundStats);

                // ── transcript observers + the crystal claim chain (handle methods) ──
                handle.onCrystalCollected = ScriptableObject.CreateInstance<ScriptableEventCrystalStats>();
                handle.crystalTriggerRadius = Mathf.Max(0.5f, options.ClaimRadius - options.VesselContactRadius);

                gameData.OnMiniGameEnd.OnRaised += handle.MarkEnded;
                readyButtonChannel.OnRaised += handle.OnReadyToggled;
                gameData.OnMiniGameTurnStarted.OnRaised += handle.OnTurnStarted;
                handle.onCrystalCollected.OnRaised += handle.HandleCrystalCollected;

                // ── kick the real flow: spawn the scene-placed NetworkBehaviours ──
                crystalMonitor.Spawn();
                handle.spawnedBehaviours.Add(crystalMonitor);
                turnMonitorController.Spawn();
                handle.spawnedBehaviours.Add(turnMonitorController);
                controller.Spawn(); // OnNetworkSpawn → config sync → InitializeAfterDelay → SetupNewRound/Turn
                handle.spawnedBehaviours.Add(controller);

                handle.Log($"round: {playerCount} AI over {gameData.RequestedDomainCount} domain(s), first domain to " +
                    $"{target} crystals wins (points: score = crystals captured), seed {options.Seed}");

                return handle;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }


        // ── course generation (seeded, HexRaceRound pattern) ────────────────────

        static Vector3[] GenerateCourse(int count)
        {
            var positions = new Vector3[count];
            var position = new Vector3(0f, 0f, 220f);
            var direction = new Vector3(0f, 0f, 1f);
            for (int i = 0; i < count; i++)
            {
                positions[i] = position;
                float yaw = Random.Range(-35f, 35f);
                float pitch = Random.Range(-12f, 12f);
                direction = (Quaternion.Euler(pitch, yaw, 0f) * direction).normalized;
                position += direction * Random.Range(150f, 230f);
            }
            return positions;
        }

        /// <summary>Same elemental cadence the SkimRace sim uses: every 7th station is Omni.</summary>
        static Element RollElement(int index)
        {
            if (index % 7 == 6) return Element.Omni;
            float roll = Random.Range(0f, 1f);
            if (roll < 0.40f) return Element.Charge;
            if (roll < 0.65f) return Element.Mass;
            if (roll < 0.85f) return Element.Space;
            return Element.Time;
        }

        /// <summary>
        /// Waypoint crystal with the real contact rig (HexRaceRound.SpawnCrystal): trigger
        /// SphereCollider + OmniCrystalImpactor (any-domain collection) + ImpactCollider
        /// routing the engine trigger pass into the impactor dispatch. The crystal removes
        /// itself on collection (Respawn → DestroyCrystal → TryRemoveItem).
        /// </summary>
        internal static Crystal SpawnCrystal(CellRuntimeDataSO courseData, CrystalManager crystalManager,
            Vector3[] course, int index,
            Element element, float triggerRadius, ScriptableEventCrystalStats onCrystalCollected)
        {
            var go = new GameObject($"crystal-{index + 1}");
            go.transform.position = course[index];

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = triggerRadius;

            var crystal = go.AddComponent<Crystal>();
            crystal.Initialize(index + 1);
            crystal.ownDomain = Domains.Blue;   // uncommitted — every domain's AI may seek it
            crystal.ItemType = ItemType.Buff;
            crystal.crystalProperties = new CrystalProperties
            {
                crystal = crystal,
                Element = element,
                crystalValue = 1f,
            };
            SetPrivateField(crystal, "cellData", courseData); // DestroyCrystal → TryRemoveItem
            crystal.InjectDependencies(crystalManager);        // NotifyManagerToExplodeCrystal → manager

            var impactor = go.AddComponent<OmniCrystalImpactor>(); // Awake binds impactor.Crystal
            SetPrivateField(impactor, "OnCrystalCollected", onCrystalCollected);

            var impactCollider = go.AddComponent<ImpactCollider>();
            SetPrivateField(impactCollider, "impactorObject", impactor);

            courseData.AddCrystalToList(crystal); // raises OnCellItemsUpdated → AIPilots retarget
            return crystal;
        }

        /// <summary>RoundStats + ResourceSystem elemental progression (the StatsManager role).</summary>
        internal static void ApplyCrystalPickup(IPlayer claimant, Element element)
        {
            var stats = claimant.RoundStats;
            stats.CrystalsCollected++;

            var resources = ((VesselController)claimant.Vessel).VesselStatus.ResourceSystem;
            if (element == Element.Omni)
            {
                stats.OmniCrystalsCollected++;
                resources.IncrementLevel(Element.Charge);
                resources.IncrementLevel(Element.Mass);
                resources.IncrementLevel(Element.Space);
                resources.IncrementLevel(Element.Time);
            }
            else
            {
                stats.ElementalCrystalsCollected++;
                resources.IncrementLevel(element);
                resources.ChangeResourceAmount(0, element == Element.Charge ? 0.3f : 0.15f);
            }
        }

        // ── vessel prefab fixture (HexRaceRound's builder, crystal-rig edition) ──

        /// <summary>
        /// Same programmatic vessel "prefab" as HexRaceRound.BuildVesselPrefab: crystal-seek
        /// AIPilot sharing the round's CellRuntimeDataSO + OnCellItemsUpdated, and the
        /// crystal contact rig — non-trigger contact bubble + VesselImpactor
        /// (+ NetworkVesselImpactor pair) + ImpactCollider — so crystal trigger contacts
        /// dispatch through the real impact pipeline on both sides.
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

            go.AddComponent<CaptureVesselAnimation>();

            var actionHandler = go.AddComponent<R_VesselActionHandler>();
            SetPrivateField(actionHandler, "_onButtonPressed", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_onButtonReleased", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>());

            var geometry = new GameObject("geometry");
            geometry.transform.SetParent(go.transform);
            var customization = go.AddComponent<VesselCustomization>();
            SetPrivateField(customization, "_shipGeometries", new List<GameObject> { geometry });

            go.AddComponent<R_ShipElementStatsHandler>();

            var hud = go.AddComponent<CaptureVesselHud>();
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

            // Contact rig (CT1): non-trigger contact bubble + impactor routing. Crystal
            // triggers pair with this collider in the engine trigger pass.
            var contactBubble = go.AddComponent<SphereCollider>();
            contactBubble.radius = contactRadius;

            var networkVesselImpactor = go.AddComponent<NetworkVesselImpactor>();
            var vesselImpactor = go.AddComponent<VesselImpactor>();
            SetPrivateField(vesselImpactor, "vesselImpactorDataContainerSO",
                ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>());
            SetPrivateField(vesselImpactor, "networkVesselImpactor", networkVesselImpactor);
            SetPrivateField(networkVesselImpactor, "vesselImpactor", vesselImpactor);

            var impactCollider = go.AddComponent<ImpactCollider>();
            SetPrivateField(impactCollider, "impactorObject", vesselImpactor);

            return go;
        }
    }
}
